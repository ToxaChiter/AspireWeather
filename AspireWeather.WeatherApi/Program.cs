using AspireWeather.Shared;
using AspireWeather.WeatherApi;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ����������� ������������
builder.AddRedisDistributedCache("cache"); // ���������� Aspire-���������� ��� ����
builder.AddRabbitMQClient("messaging");
builder.Services.AddHttpClient<UserApiClient>(client =>
{
    // ��� "userapi" ����� ��������� Aspire � ���������� �����
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
    // 1. ���������� �����: �������� ���������� � ������������
    logger.LogInformation("������ ���������� ��� ������������ {UserId}", userId);
    var user = await userApiClient.GetUserAsync(userId);
    if (user is null)
    {
        logger.LogWarning("������������ � ID {UserId} �� ������", userId);
        return Results.NotFound(new { message = $"User with id {userId} not found." });
    }

    // 2. ������ � �����: ���������, ���� �� ������� � ����
    var cacheKey = $"forecast-{user.Location}";
    var cachedForecast = await cache.GetStringAsync(cacheKey);

    if (!string.IsNullOrEmpty(cachedForecast))
    {
        logger.LogInformation(">>> ������� ��� {Location} ������ � ���� Redis", user.Location);
        var forecastFromCache = JsonSerializer.Deserialize<WeatherForecast[]>(cachedForecast)!;
        return Results.Ok(forecastFromCache);
    }

    logger.LogInformation("--- ������� ��� {Location} � ���� �� ������. ���������� �����.", user.Location);

    // 3. ����������� ����������: ���������� ������� � RabbitMQ
    try
    {
        using var channel = rabbitConnection.CreateModel();
        const string queueName = "forecast-requests";
        channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        var message = new ForecastRequestedEvent(user.Id, user.Location, DateTime.UtcNow);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
        logger.LogInformation("������������ ������� ForecastRequestedEvent � ������� {QueueName}", queueName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "�� ������� ������������ ��������� � RabbitMQ");
        // �� ��������� ����������, ����� - �� ��������� ��������
    }

    // 4. ��������� ������: ������� ����� �������
    await Task.Delay(200); // �������� ������
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)],
            user.Location,
            user.Name
        )).ToArray();

    // 5. ���������� � ���: ������ ����� ������� � Redis �� 10 ������
    var cacheOptions = new DistributedCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromSeconds(10));
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(forecast), cacheOptions);
    logger.LogInformation("����� ������� ��� {Location} �������� � ���", user.Location);

    return Results.Ok(forecast);
});

app.MapDefaultEndpoints();

app.Run();