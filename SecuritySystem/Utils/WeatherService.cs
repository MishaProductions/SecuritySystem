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
            try
            {
                if (string.IsNullOrEmpty(Configuration.Instance.WeatherCords))
                {
                    InternetWeather = "Weather cords not set in config";
                    return;
                }
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-agent", "SecuritySystem-app");
                var data = await httpClient.GetAsync("https://api.weather.gov/points/" + Configuration.Instance.WeatherCords);
                if (data.IsSuccessStatusCode)
                {
                    dynamic fs = JObject.Parse(await data.Content.ReadAsStringAsync());
                    var url = (string)fs.properties.forecastHourly;

                    var data2 = await httpClient.GetAsync(url);
                    if (data2.IsSuccessStatusCode)
                    {
                        dynamic fs2 = JObject.Parse(await data2.Content.ReadAsStringAsync());

                        var today = fs2.properties.periods[0];
                        InternetWeather = "";
                        if (today.temperature != null)
                        {
                            InternetWeather += $"Temperature: {(int)today.temperature}\r\n";
                        }
                        if (today.shortForecast != null)
                        {
                            InternetWeather += $"{(string)today.shortForecast}\r\n";
                        }
                        if (today.probabilityOfPrecipitation.value != null)
                        {
                            InternetWeather += $"Participation chance: {(int)today.probabilityOfPrecipitation.value}%\r\n";
                        }
                        LastUpdateTime = DateTime.Now;
                    }
                    else
                    {
                        InternetWeather = "weather.gov failed to get forecast info with code " + data2.StatusCode;
                        Console.WriteLine("weather: failed to get forecast api");
                    }
                }
                else
                {
                    InternetWeather = "weather.gov get point api FAIL";
                    Console.WriteLine("weather: failed to get point api");
                }


                Console.WriteLine("[weather] internet weather result: " + InternetWeather);
            }
            catch (Exception ex)
            {
                InternetWeather = "failed to load weather info:\r\n" + ex.Message;
            }
        }

        private static async Task UpdateTempSensorInfo()
        {
            TempWeather = "";
            Console.WriteLine("[weather] query temp info");
            foreach (var busId in OneWireBus.EnumerateBusIds())
            {
                OneWireBus bus = new OneWireBus(busId);
                foreach (var devId in bus.EnumerateDeviceIds())
                {
                    if (OneWireThermometerDevice.IsCompatible(busId, devId))
                    {
                        OneWireThermometerDevice devTemp = new(busId, devId);
                        string temp = (await devTemp.ReadTemperatureAsync()).DegreesFahrenheit.ToString("F2") + "F";

                        TempWeather += $"Temperature sensor: {temp}\r\n";
                    }
                }
            }
        }
        public static async Task<string> GetWeather()
        {
            try
            {
                if ((DateTime.Now - LastUpdateTime).Minutes >= 2)
                {
                    await UpdateTempSensorInfo();
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
}
