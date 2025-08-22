using AspireWeather.AuditService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddRabbitMQClient("messaging");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();