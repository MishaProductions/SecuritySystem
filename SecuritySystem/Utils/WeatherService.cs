using Iot.Device.OneWire;
using Newtonsoft.Json.Linq;

namespace SecuritySystem.Utils
{
    internal class WeatherService
    {
        private static string InternetWeather = "";
        private static string TempWeather = "";
        private static DateTime LastUpdateTime = DateTime.MinValue;
        
        private static async Task UpdateWeatherInfo()
        {
            if (string.IsNullOrEmpty(Configuration.Instance.WeatherCords))
            {
                InternetWeather = "Weather cords not set in config";
                return;
            }

            string result = "";
            try
            {
                HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.Add("User-agent", "SecuritySystem (https://github.com/MishaProductions/SecuritySystem)");
                var data = await httpClient.GetAsync("https://api.weather.gov/points/" + Configuration.Instance.WeatherCords);
                if (data.IsSuccessStatusCode)
                {
                    dynamic fs = JObject.Parse(await data.Content.ReadAsStringAsync());
                    var url = (string)fs.properties.forecastHourly;

                    var data2 = await httpClient.GetAsync(url);
                    dynamic fs2 = JObject.Parse(await data2.Content.ReadAsStringAsync());
                    if (data2.IsSuccessStatusCode)
                    {
                        var today = fs2.properties.periods[0];
                        if (today.temperature != null)
                        {
                            result += $"Temperature: {(int)today.temperature}\r\n";
                        }
                        if (today.shortForecast != null)
                        {
                            result += $"{(string)today.shortForecast}\r\n";
                        }
                        if (today.probabilityOfPrecipitation.value != null)
                        {
                            result += $"Participation chance: {(int)today.probabilityOfPrecipitation.value}%\r\n";
                        }
                        //result += (string)today.detailedForecast;
                        LastUpdateTime = DateTime.Now;
                    }
                    else
                    {
                        result = "weather.gov failed to get forecast info with code:\r\n" + (string)fs2.detail + "\r\n";
                        Console.WriteLine("weather: failed to get forecast api");
                    }
                }
                else
                {
                    result = "weather.gov get point api FAIL\r\n";
                    Console.WriteLine("weather: failed to get point api");
                }


                Console.WriteLine("[weather] internet weather result: " + result);
            }
            catch (Exception ex)
            {
                result = "failed to load weather info:\r\n" + ex.Message + "\r\n";
            }

            InternetWeather = result;
        }

        private static async Task UpdateTempSensorInfo()
        {
            string result = "";
            Console.WriteLine("[weather] query temp info");
            foreach (var busId in OneWireBus.EnumerateBusIds())
            {
                OneWireBus bus = new(busId);
                foreach (var devId in bus.EnumerateDeviceIds())
                {
                    if (OneWireThermometerDevice.IsCompatible(busId, devId))
                    {
                        OneWireThermometerDevice devTemp = new(busId, devId);
                        string temp = (await devTemp.ReadTemperatureAsync()).DegreesFahrenheit.ToString("F2") + "F";

                        result += $"Temperature sensor: {temp}\r\n";
                    }
                }
            }

            TempWeather = result;
        }
        public static async Task<string> GetWeather()
        {
            try
            {
                if ((DateTime.Now - LastUpdateTime).Minutes >= 2)
                {
                    //await UpdateTempSensorInfo();
                }

                if ((DateTime.Now - LastUpdateTime).Hours >= 1)
                {
                    await UpdateWeatherInfo();
                }

                string result = InternetWeather + TempWeather;
                Console.WriteLine("[weather]: returning " + result);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[weather]: exception: " + ex.ToString());
                return "ERROR WHILE QUERY";
            }
        }
    }

    public class PointResponse
    {
        public PointResponseProperties? properties { get; set;}
    }
    public class PointResponseProperties
    {
        public string forecast { get; set; } = "";
        public string forecastHourly { get; set; } = "";
    }
}
