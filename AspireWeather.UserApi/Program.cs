using AspireWeather.Shared;
using AspireWeather.UserApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<UserDbContext>("userdb");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Автоматически применяем миграции и сидируем данные при старте (только для демо!)
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    await db.Database.MigrateAsync();

    // Добавляем тестовых пользователей, если их нет
    if (!await db.Users.AnyAsync())
    {
        db.Users.AddRange(
            new User { Id = 1, Name = "Анна", Location = "Москва" },
            new User { Id = 2, Name = "Борис", Location = "Санкт-Петербург" },
            new User { Id = 3, Name = "Виктория", Location = "Новосибирск" }
        );
        await db.SaveChangesAsync();
    }
}


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/users", async (UserDbContext db) =>
{
    var users = await db.Users
        .Select(u => new UserDto(u.Id, u.Name, u.Location))
        .ToListAsync();
    return Results.Ok(users);
});

app.MapGet("/users/{id}", async (int id, UserDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    return user is not null
        ? Results.Ok(new UserDto(user.Id, user.Name, user.Location))
        : Results.NotFound();
});

app.MapDefaultEndpoints();

app.Run();