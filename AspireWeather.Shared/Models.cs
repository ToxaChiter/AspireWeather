using System.Text.Json.Serialization;

namespace AspireWeather.Shared;

// DTO для UserApi
public record UserDto(int Id, string Name, string Location);

// DTO для WeatherApi
public record WeatherForecast(
    DateOnly Date,
    int TemperatureC,
    string? Summary,
    string Location,
    [property: JsonPropertyName("prepared_for")] string PreparedFor
);

// Событие для RabbitMQ
public record ForecastRequestedEvent(int UserId, string Location, DateTime Timestamp);