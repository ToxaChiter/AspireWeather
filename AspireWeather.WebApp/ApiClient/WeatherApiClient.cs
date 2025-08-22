using AspireWeather.Shared;

namespace AspireWeather.WebApp.ApiClient;

public class WeatherApiClient(HttpClient httpClient)
{
    public async Task<WeatherForecast[]?> GetWeatherForecastAsync(int userId) =>
        await httpClient.GetFromJsonAsync<WeatherForecast[]>($"/weatherforecast/{userId}");
}