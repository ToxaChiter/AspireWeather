using AspireWeather.Shared;

namespace AspireWeather.WeatherApi;

public class UserApiClient(HttpClient httpClient)
{
    public async Task<UserDto?> GetUserAsync(int userId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<UserDto>($"/users/{userId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}