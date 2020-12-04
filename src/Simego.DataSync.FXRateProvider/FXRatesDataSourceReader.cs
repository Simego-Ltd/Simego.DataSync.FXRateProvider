using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Xml;
using Simego.DataSync.Providers;

namespace Simego.DataSync.FXRateProvider
{
    [ProviderInfo(Name = "FX Rates", Description = "Read Foreign Exchange Rates from EU Reference Rates http://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml")]
    public class FXRatesDataSourceReader : DataReadOnlyReaderProviderBase
    {                        
        [Category("Connection")]
        [Description("The base Currency")]
        public string BaseCurrency { get; set; }
        
        #region IDataSourceReader Members

        public FXRatesDataSourceReader()           
        {
            BaseCurrency = "USD";
        }

        public override DataTableStore GetDataTable(DataTableStore dt)
        {            
            //Create a Schema Mapping Helper
            DataSchemaMapping mapping = new DataSchemaMapping(SchemaMap, Side);
            //Get the Rates from Yahoo
            Dictionary<string, double> rates = GetFXRates();
            
            //Calculate the Conversion Rate from other base currency
            double conversion_rate = 1 / rates[BaseCurrency];

            //Create a Sorted List of Rates
            var rateList = new List<string>();
            foreach (string k in rates.Keys)
                rateList.Add(k);
            rateList.Sort();

            //Populate the Rates Table
            foreach (string k in rateList)
            {
                var newRow = dt.NewRow();
                foreach (DataSchemaItem item in SchemaMap.GetIncludedColumns())
                {
                    string columnName = mapping.MapColumnToDestination(item);
                    switch (columnName)
                    {
                        case "Currency":
                            {
                                newRow[item.ColumnName] = DataSchemaTypeConverter.ConvertTo(k, item.DataType); 
                                break;
                            }
                        case "Rate":
                            {
                                newRow[item.ColumnName] = DataSchemaTypeConverter.ConvertTo(Math.Round(rates[k] * conversion_rate, 4), item.DataType);
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }                    
                }

                if (dt.Rows.Add(newRow) == DataTableStore.ABORT)
                    break;

            }

            return dt;
        }
        
        public override DataSchema GetDefaultDataSchema()
        {
            DataTable dt = new DataTable("FXRates");
            dt.Columns.Add("Currency", typeof(string));
            dt.Columns.Add("Rate", typeof(double));

            dt.Columns["Currency"].AllowDBNull = false;
            dt.Columns["Currency"].MaxLength = 3;
            dt.Columns["Currency"].Unique = true;
            dt.Columns["Rate"].AllowDBNull = false;

            return new DataSchema(dt);
        }
        
        public override List<ProviderParameter> GetInitializationParameters()
        {
            List<ProviderParameter> parameters = new List<ProviderParameter>();

            parameters.Add(new ProviderParameter("BaseCurrency", BaseCurrency));
            
            return parameters;
        }

        public override void Initialize(List<ProviderParameter> parameters)
        {
            foreach (ProviderParameter p in parameters)
            {                
                switch (p.Name)
                {
                    case "BaseCurrency":
                        {
                            BaseCurrency = p.Value;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }
        }

        #endregion      
        
        private static Dictionary<string, double> GetFXRates()
        {
            Dictionary<string, double> rates = new Dictionary<string, double>();

            WebRequest request = WebRequest.Create("http://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml");
            
            using (var response = request.GetResponse())
            {
                XmlReader reader = null;
                try
                {
                    reader = new XmlTextReader(response.GetResponseStream());

                    //Add the Default EUR Rate
                    rates.Add("EUR", 1);

                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name.Equals("Cube", StringComparison.OrdinalIgnoreCase))
                            {
                                var ccy = reader.GetAttribute("currency");
                                var price = Convert.ToDouble(reader.GetAttribute("rate"));

                                if (ccy != null && Math.Abs(price) > double.Epsilon)
                                {
                                    rates[ccy] = price;
                                }
                            }                            
                        }
                    }
                }
                finally
                {
                    if (reader != null)
                        reader.Close();
                }
            }

            return rates;
        } 
    }
}
