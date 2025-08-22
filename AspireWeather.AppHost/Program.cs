using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// --- ������� ����������� (����������) ---
var cache = builder.AddRedis("cache");
var postgres = builder.AddPostgres("postgres");

var username = builder.AddParameter("username", secret: true);
var password = builder.AddParameter("password", secret: true);

// ������� RabbitMQ � ����� ������������� � �������
var messaging = builder.AddRabbitMQ("messaging", username, password);


// --- ���� ������ ---
// ������� ���� ������ "userdb" � ����� ���������� Postgres
var userDb = postgres.AddDatabase("userdb");

// --- ������� ---

// UserApi ������� ������ �� ����� ���� ������
var userApi = builder.AddProject<Projects.AspireWeather_UserApi>("userapi")
                     .WithReference(userDb)
                     .WaitFor(postgres);

// WeatherApi ������� �� ����, ������� ��������� � �� UserApi
var weatherApi = builder.AddProject<Projects.AspireWeather_WeatherApi>("weatherapi")
                        .WithReference(cache)
                        .WithReference(messaging)
                        .WithReference(userApi)
                        .WaitFor(messaging);

// AuditService ������� ������ �� ������� ���������
var auditService = builder.AddProject<Projects.AspireWeather_AuditService>("auditservice")
                          .WithReference(messaging)
                          .WaitFor(messaging);

// WebApp (UI) ������� �� ����� API ��� ��������� ������
builder.AddProject<Projects.AspireWeather_WebApp>("webapp")
       .WithReference(weatherApi)
       .WithReference(userApi);

builder.Build().Run();