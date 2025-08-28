// The 'RabbitMQ.Client' namespace is required for IConnection and related types
using RabbitMQ.Client;
// The 'RabbitMQ.Client.Events' namespace is required for EventingBasicConsumer
using RabbitMQ.Client.Events;
// The 'System.Text' namespace is required for Encoding
using System.Text;
// The 'System.Text.Json' namespace is required for JsonSerializer
using System.Text.Json;


namespace AspireWeather.AuditService;



// A sample event class to make the code compilable.
// Replace with your actual ForecastRequestedEvent definition.
public class ForecastRequestedEvent
{
    // Меняем string на int (или long, если ID может быть очень большим)
    public int UserId { get; set; }
    public string? Location { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// A background service that listens for messages on a RabbitMQ queue.
/// </summary>
public class Worker(ILogger<Worker> logger, IConnection rabbitConnection) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // When the stopping token is cancelled, it means the application is shutting down.
        // We register a log message to know when this happens.
        stoppingToken.Register(() => logger.LogInformation("Worker service is stopping."));

        try
        {
            // Using a 'using' statement ensures that the channel is properly disposed of
            // when the service stops, preventing resource leaks.
            using var channel = rabbitConnection.CreateModel();

            // It's a good practice to get queue names from configuration instead of hardcoding.
            const string queueName = "forecast-requests";

            // Declare the queue to ensure it exists. This is an idempotent operation.
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
                            ">>> [AUDIT] Received event: User {UserId} requested a forecast for '{Location}' at {Timestamp}",
                            message.UserId, message.Location, message.Timestamp);
                    }
                    else
                    {
                        logger.LogWarning("Received a message that deserialized to null.");
                    }

                    // Manually acknowledge the message after successful processing.
                    // This tells RabbitMQ that the message has been handled and can be safely deleted.
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Error deserializing message from queue: {Message}", Encoding.UTF8.GetString(body));
                    // Reject the message without re-queueing it, as it's likely malformed and will fail again.
                    // Consider sending it to a Dead-Letter-Exchange (DLX) for later inspection.
                    channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred while processing a message.");
                    // Reject the message. Depending on the error, you might want to requeue it.
                    // Setting requeue to 'false' is safer to prevent infinite processing loops for poison messages.
                    channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            // Start consuming messages.
            // 'autoAck' is set to 'false' for manual acknowledgment, which prevents message loss.
            channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

            logger.LogInformation("Queue listener for {QueueName} started", queueName);

            // This loop keeps the background service running until the application is shut down.
            // The actual message processing happens on the RabbitMQ client's threads.
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected when the application is shutting down.
            // No need to log it as an error.
            logger.LogInformation("Worker service has been cancelled gracefully.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to start RabbitMQ listener. The service will not process messages.");
        }
    }
}