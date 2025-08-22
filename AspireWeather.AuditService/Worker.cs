using System.Text;
using System.Text.Json;
using AspireWeather.Shared;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AspireWeather.AuditService;

public class Worker(ILogger<Worker> logger, IConnection rabbitConnection) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            const string queueName = "forecast-requests";
            var channel = rabbitConnection.CreateModel();

            channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                try
                {
                    var message = JsonSerializer.Deserialize<ForecastRequestedEvent>(body);
                    if (message != null)
                    {
                        logger.LogInformation(
                            ">>> [AUDIT] �������� �������: ������������ {UserId} �������� ������� ��� '{Location}' � {Timestamp}",
                            message.UserId, message.Location, message.Timestamp);
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "������ �������������� ��������� �� �������: {Message}", Encoding.UTF8.GetString(body));
                }
            };

            channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);
            logger.LogInformation("��������� ������� {QueueName} �������", queueName);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "�� ������� ��������� ��������� RabbitMQ. ������ �� ����� ������������ ���������.");
        }

        return Task.CompletedTask;
    }
}