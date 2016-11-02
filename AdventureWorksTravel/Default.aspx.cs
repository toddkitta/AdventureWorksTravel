using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Specialized;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace AdventureWorksTravel
{
    public partial class _Default : Page
    {
        private const string DEFAULT_ML_SERVICE_LOCATION = "ussouthcentral";
        // updated azure ml endpoint url
        private const string BASE_ML_URI = "https://{0}.services.azureml.net/subscriptions/{1}/services/{2}/execute?api-version=2.0&details=true";
        private const string BASE_WEATHER_URI = "http://api.wunderground.com/api/{0}/hourly10day/q/{1}/{2}.json";

        private List<Airport> aiports = null;
        private ForecastResult forecast = null;
        private DelayPrediction prediction = null;

        // settings
        private string mlApiKey;
        private string mlWorkspaceId;
        private string mlServiceId;
        private string weatherApiKey;
        private string mlServiceLocation;

        protected void Page_Load(object sender, EventArgs e)
        {
            InitSettings();
            InitAirports();

            if (!IsPostBack)
            {
                txtDepartureDate.Text = DateTime.Now.AddDays(5).ToShortDateString();

                ddlOriginAirportCode.DataSource = aiports;
                ddlOriginAirportCode.DataTextField = "AirportCode";
                ddlOriginAirportCode.DataValueField = "AirportState";  
                ddlOriginAirportCode.DataBind();

                ddlDestAirportCode.DataSource = aiports;
                ddlDestAirportCode.DataTextField = "AirportCode";
                ddlDestAirportCode.DataValueField = "AirportState"; 
                ddlDestAirportCode.DataBind();
                ddlDestAirportCode.SelectedIndex = 12;
            }
        }

        private void InitSettings()
        {
            mlApiKey = System.Web.Configuration.WebConfigurationManager.AppSettings["mlApiKey"];
            mlWorkspaceId = System.Web.Configuration.WebConfigurationManager.AppSettings["mlWorkspaceId"];
            mlServiceId = System.Web.Configuration.WebConfigurationManager.AppSettings["mlServiceId"];
            weatherApiKey = System.Web.Configuration.WebConfigurationManager.AppSettings["weatherApiKey"];
            mlServiceLocation = System.Web.Configuration.WebConfigurationManager.AppSettings["mlServiceLocation"];
        }

        private void InitAirports()
        {
            aiports = new List<Airport>()
            {
                new Airport() { AirportCode ="SEA", AirportWundergroundID="SEA" , AirportState="WA" },
                new Airport() { AirportCode ="ABQ", AirportWundergroundID="ABQ" , AirportState="NM" },
                new Airport() { AirportCode ="ANC", AirportWundergroundID="ANC" , AirportState="AK"},
                new Airport() { AirportCode ="ATL", AirportWundergroundID="ATL" , AirportState="GA"},
                new Airport() { AirportCode ="AUS", AirportWundergroundID="AUS" , AirportState="TX"},
                new Airport() { AirportCode ="CLE", AirportWundergroundID="CLE" , AirportState="OH"},
                new Airport() { AirportCode ="DTW", AirportWundergroundID="DTW" , AirportState="MI"},
                new Airport() { AirportCode ="JAX", AirportWundergroundID="JAX" , AirportState="FL" },
                new Airport() { AirportCode ="MEM", AirportWundergroundID="MEM" , AirportState="TN"},
                new Airport() { AirportCode ="MIA", AirportWundergroundID="MIA" , AirportState="FL"},
                new Airport() { AirportCode ="ORD", AirportWundergroundID="zmw:60666.6.99999" , AirportState="IL"},
                new Airport() { AirportCode ="PHX", AirportWundergroundID="PHX" , AirportState="AZ"},
                new Airport() { AirportCode ="SAN", AirportWundergroundID="zmw:92140.5.99999" , AirportState="CA"},
                new Airport() { AirportCode ="SFO", AirportWundergroundID="SFO" , AirportState="CA"},
                new Airport() { AirportCode ="SJC", AirportWundergroundID="SJC" , AirportState="CA"},
                new Airport() { AirportCode ="SLC", AirportWundergroundID="SLC" , AirportState="UT"},
                new Airport() { AirportCode ="STL", AirportWundergroundID="STL" , AirportState="MO"},
                new Airport() { AirportCode ="TPA", AirportWundergroundID="TPA"  , AirportState="FL"}
            };
        }

        protected void btnPredictDelays_Click(object sender, EventArgs e)
        {
            var departureDate = DateTime.Parse(txtDepartureDate.Text);
            departureDate.AddHours(double.Parse(txtDepartureHour.Text));

            var selectedAirport = ddlOriginAirportCode.SelectedItem;

            DepartureQuery query = new DepartureQuery()
            {
                DepartureDate = departureDate,
                DepartureDayOfWeek = ((int)departureDate.DayOfWeek) + 1, //Monday = 1
                Carrier = txtCarrier.Text,
                OriginAirportCode = ddlOriginAirportCode.SelectedItem.Text,
                OriginAirportWundergroundID =  ddlOriginAirportCode.SelectedItem.Text, 
                OriginAirportState = ddlOriginAirportCode.SelectedItem.Value,
                DestAirportCode = ddlDestAirportCode.SelectedItem.Text
            };

            GetWeatherForecast(query).Wait();

            if (forecast == null)
                throw new Exception("Forecast request did not succeed. Check Settings for weatherApiKey.");

            PredictDelays(query, forecast).Wait();

            UpdateStatusDisplay(prediction, forecast);
        }

        private void UpdateStatusDisplay(DelayPrediction prediction, ForecastResult forecast)
        {
            weatherForecast.ImageUrl = forecast.ForecastIconUrl;
            weatherForecast.ToolTip = forecast.Condition;

            if (String.IsNullOrWhiteSpace(mlApiKey))
            {
                lblPrediction.Text = "(not configured)";
                lblConfidence.Text = "(not configured)";
                return;
            }

            if (prediction == null)
                throw new Exception("Prediction did not succeed. Check the Settings for mlWorkspaceId, mlServiceId, and mlApiKey.");

            if (prediction.ExpectDelays)
            {
                lblPrediction.Text = "expect delays";
            }
            else
            {
                lblPrediction.Text = "no delays expected";
            }

            lblConfidence.Text = string.Format("{0:N2}", (prediction.Confidence * 100.0));
        }

        private async Task GetWeatherForecast(DepartureQuery departureQuery)
        {
            DateTime departureDate = departureQuery.DepartureDate;
            string fullWeatherURI = string.Format(BASE_WEATHER_URI, weatherApiKey, departureQuery.OriginAirportState ,departureQuery.OriginAirportWundergroundID);
            forecast = null;

            try
            {
                using (var client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(fullWeatherURI).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        JObject jsonObj = JObject.Parse(result);

                        forecast = (from f in jsonObj["hourly_forecast"]
                                    where f["FCTTIME"]["year"].Value<int>() == departureDate.Year &&
                                          f["FCTTIME"]["mon"].Value<int>() == departureDate.Month &&
                                          f["FCTTIME"]["mday"].Value<int>() == departureDate.Day &&
                                          f["FCTTIME"]["hour"].Value<int>() == departureDate.Hour
                                    select new ForecastResult()
                                    {
                                        WindSpeed = f["wspd"]["english"].Value<int>(),
                                        Precipitation = f["qpf"]["english"].Value<double>(),
                                        Pressure = f["mslp"]["english"].Value<double>(),
                                        ForecastIconUrl = f["icon_url"].Value<string>(),
                                        Condition = f["condition"].Value<string>()
                                    }).FirstOrDefault();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Failed retrieving weather forecast: " + ex.ToString());
            }
        }

        private async Task PredictDelays(DepartureQuery query, ForecastResult forecast)
        {
            if (String.IsNullOrEmpty(mlApiKey))
            {
                return;
            }

            string fullMLUri = string.Format(BASE_ML_URI, !String.IsNullOrWhiteSpace(mlServiceLocation) ? mlServiceLocation : DEFAULT_ML_SERVICE_LOCATION, mlWorkspaceId, mlServiceId);
            var departureDate = DateTime.Parse(txtDepartureDate.Text);

            prediction = new DelayPrediction();

            try
            {
                using (var client = new HttpClient())
                {
                    var scoreRequest = new
                    {
                        Inputs = new Dictionary<string, StringTable>()
                        {
                            {
                                "input1",
                                new StringTable()
                                {
                                    ColumnNames = new string[]
                                    {
                                        "OriginAirportCode",
                                        "Month",
                                        "DayofMonth",
                                        "CRSDepHour",
                                        "DayOfWeek",
                                        "Carrier",
                                        "DestAirportCode",
                                        "WindSpeed",
                                        "SeaLevelPressure",
                                        "HourlyPrecip"
                                    },
                                    Values = new string[,]
                                    {
                                        {
                                            query.OriginAirportCode,
                                            query.DepartureDate.Month.ToString(),
                                            query.DepartureDate.Day.ToString(),
                                            query.DepartureDate.Hour.ToString(),
                                            query.DepartureDayOfWeek.ToString(),
                                            query.Carrier,
                                            query.DestAirportCode,
                                            forecast.WindSpeed.ToString(),
                                            forecast.Pressure.ToString(),
                                            forecast.Precipitation.ToString()
                                        }
                                    }
                                }
                            },
                        },
                        GlobalParameters = new Dictionary<string, string>()
                        {
                        }
                    };

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mlApiKey);
                    client.BaseAddress = new Uri(fullMLUri);
                    HttpResponseMessage response = await client.PostAsJsonAsync("", scoreRequest).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        JObject jsonObj = JObject.Parse(result);

                        string prediction = jsonObj["Results"]["output1"]["value"]["Values"][0][10].ToString();
                        string confidence = jsonObj["Results"]["output1"]["value"]["Values"][0][11].ToString();

                        if (prediction.Equals("1"))
                        {
                            this.prediction.ExpectDelays = true;
                            this.prediction.Confidence = double.Parse(confidence);
                        }
                        else if (prediction.Equals("0"))
                        {
                            this.prediction.ExpectDelays = false;
                            this.prediction.Confidence = double.Parse(confidence);
                        }
                        else
                        {
                            this.prediction = null;
                        }

                    }
                    else
                    {
                        prediction = null;

                        Trace.Write(string.Format("The request failed with status code: {0}", response.StatusCode));

                        // Print the headers - they include the request ID and the timestamp, which are useful for debugging the failure
                        Trace.Write(response.Headers.ToString());

                        string responseContent = await response.Content.ReadAsStringAsync();
                        Trace.Write(responseContent);
                    }
                }
            }
            catch (Exception ex)
            {
                prediction = null;
                System.Diagnostics.Trace.TraceError("Failed retrieving delay prediction: " + ex.ToString());
            }
        }
    }

    #region Data Structures

    public class StringTable
    {
        public string[] ColumnNames { get; set; }
        public string[,] Values { get; set; }
    }

    public class ForecastResult
    {
        public int WindSpeed;
        public double Precipitation;
        public double Pressure;
        public string ForecastIconUrl;
        public string Condition;
    }

    public class DelayPrediction
    {
        public bool ExpectDelays;
        public double Confidence;
    }

    public class DepartureQuery
    {
        public string OriginAirportCode;
        public string OriginAirportWundergroundID;
        public string OriginAirportState;
        public string DestAirportCode;
        public DateTime DepartureDate;
        public int DepartureDayOfWeek;
        public string Carrier;
    }

    public class Airport
    {
        public string AirportCode { get; set; }
        public string AirportWundergroundID { get; set; }
        public string AirportState { get; set; }
    }

    #endregion
}