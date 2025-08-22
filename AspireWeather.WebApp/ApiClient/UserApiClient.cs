using AspireWeather.Shared;

namespace AspireWeather.WebApp.ApiClient;

public class UserApiClient(HttpClient httpClient)
{
    public async Task<UserDto[]> GetUsersAsync() =>
        await httpClient.GetFromJsonAsync<UserDto[]>("/users") ?? [];
}