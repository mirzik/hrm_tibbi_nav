using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TibbiNav.Infrastructure;

/// <summary>
/// Design-time factory для `dotnet ef migrations add/update`.
/// Нужна отдельно от Program.cs, чтобы EF-тулинг не поднимал весь ASP.NET
/// хост (Swagger, Auth, CORS и т.д.) только ради построения модели.
/// Строка подключения та же, что и в appsettings.json (dev default),
/// но может быть переопределена переменной окружения ConnectionStrings__Default —
/// именно так её задаёт docker-compose.yml для контейнера api.
/// </summary>
public class TibbiNavDbContextFactory : IDesignTimeDbContextFactory<TibbiNavDbContext>
{
    public TibbiNavDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=tibbinav_hrm;Username=tibbinav;Password=CHANGE_ME";

        var optionsBuilder = new DbContextOptionsBuilder<TibbiNavDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new TibbiNavDbContext(optionsBuilder.Options);
    }
}
