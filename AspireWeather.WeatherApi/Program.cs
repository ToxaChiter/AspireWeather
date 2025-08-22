using AspireWeather.Shared;
using AspireWeather.WeatherApi;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Регистрация зависимостей
builder.AddRedisDistributedCache("cache"); // Используем Aspire-расширение для кэша
builder.AddRabbitMQClient("messaging");
builder.Services.AddHttpClient<UserApiClient>(client =>
{
    // Имя "userapi" будет разрешено Aspire в правильный адрес
    client.BaseAddress = new("http://userapi");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast/{userId}", async (
    int userId,
    IDistributedCache cache,
    IConnection rabbitConnection,
    UserApiClient userApiClient,
    ILogger<Program> logger) =>
{
    // 1. СИНХРОННЫЙ ВЫЗОВ: Получаем информацию о пользователе
    logger.LogInformation("Запрос информации для пользователя {UserId}", userId);
    var user = await userApiClient.GetUserAsync(userId);
    if (user is null)
    {
        logger.LogWarning("Пользователь с ID {UserId} не найден", userId);
        return Results.NotFound(new { message = $"User with id {userId} not found." });
    }

    // 2. РАБОТА С КЭШЕМ: Проверяем, есть ли прогноз в кэше
    var cacheKey = $"forecast-{user.Location}";
    var cachedForecast = await cache.GetStringAsync(cacheKey);

    if (!string.IsNullOrEmpty(cachedForecast))
    {
        logger.LogInformation(">>> Прогноз для {Location} найден в кэше Redis", user.Location);
        var forecastFromCache = JsonSerializer.Deserialize<WeatherForecast[]>(cachedForecast)!;
        return Results.Ok(forecastFromCache);
    }

    logger.LogInformation("--- Прогноз для {Location} в кэше не найден. Генерируем новый.", user.Location);

    // 3. АСИНХРОННАЯ ПУБЛИКАЦИЯ: Отправляем событие в RabbitMQ
    try
    {
        using var channel = rabbitConnection.CreateModel();
        const string queueName = "forecast-requests";
        channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        var message = new ForecastRequestedEvent(user.Id, user.Location, DateTime.UtcNow);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
        logger.LogInformation("Опубликовано событие ForecastRequestedEvent в очередь {QueueName}", queueName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Не удалось опубликовать сообщение в RabbitMQ");
        // Не прерываем выполнение, аудит - не критичная операция
    }

    // 4. ГЕНЕРАЦИЯ ДАННЫХ: Создаем новый прогноз
    await Task.Delay(200); // Имитация работы
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)],
            user.Location,
            user.Name
        )).ToArray();

    // 5. СОХРАНЕНИЕ В КЭШ: Кладем новый прогноз в Redis на 10 секунд
    var cacheOptions = new DistributedCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromSeconds(10));
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(forecast), cacheOptions);
    logger.LogInformation("Новый прогноз для {Location} сохранен в кэш", user.Location);

    return Results.Ok(forecast);
});

app.MapDefaultEndpoints();

app.Run();