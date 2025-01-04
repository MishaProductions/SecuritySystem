using SecuritySystemApi;

namespace MHSApi.API
{
    public class ShortWeatherDataContent : ApiResponseContent
    {
        public string WeatherData { get; set; } = "";
        public ShortWeatherDataContent(string weatherData) => WeatherData = weatherData;
        public ShortWeatherDataContent() {}
    }
}
