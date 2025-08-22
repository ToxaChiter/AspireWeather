using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// --- Внешние зависимости (контейнеры) ---
var cache = builder.AddRedis("cache");
var postgres = builder.AddPostgres("postgres");

var username = builder.AddParameter("username", secret: true);
var password = builder.AddParameter("password", secret: true);

// Создаем RabbitMQ с явным пользователем и паролем
var messaging = builder.AddRabbitMQ("messaging", username, password);


// --- Базы данных ---
// Создаем базу данных "userdb" в нашем контейнере Postgres
var userDb = postgres.AddDatabase("userdb");

// --- Сервисы ---

// UserApi зависит только от своей базы данных
var userApi = builder.AddProject<Projects.AspireWeather_UserApi>("userapi")
                     .WithReference(userDb)
                     .WaitFor(postgres);

// WeatherApi зависит от кэша, брокера сообщений и от UserApi
var weatherApi = builder.AddProject<Projects.AspireWeather_WeatherApi>("weatherapi")
                        .WithReference(cache)
                        .WithReference(messaging)
                        .WithReference(userApi)
                        .WaitFor(messaging);

// AuditService зависит только от брокера сообщений
var auditService = builder.AddProject<Projects.AspireWeather_AuditService>("auditservice")
                          .WithReference(messaging)
                          .WaitFor(messaging);

// WebApp (UI) зависит от обоих API для получения данных
builder.AddProject<Projects.AspireWeather_WebApp>("webapp")
       .WithReference(weatherApi)
       .WithReference(userApi);

builder.Build().Run();