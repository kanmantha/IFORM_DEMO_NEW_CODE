using IForm.Application.Common.Interfaces;
using IForm.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IForm.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=IFORM_SiteQuery;Trusted_Connection=True;MultipleActiveResultSets=true";

        var provider = configuration["Database:Provider"] ?? "SqlServer";
        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connectionString);
            else
                options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        return services;
    }
}
