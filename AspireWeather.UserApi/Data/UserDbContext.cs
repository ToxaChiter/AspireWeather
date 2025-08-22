using Microsoft.EntityFrameworkCore;

namespace AspireWeather.UserApi.Data;

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Location { get; set; }
}

public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
}