using Dsw2025Tpi.Api;
using Dsw2025Tpi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

// Levanta la API completa en memoria (entorno "Testing") con SQLite en lugar de SQL Server.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminUsername = "admin-tp4";
    public const string AdminEmail = "admin-tp4@test.local";
    public const string AdminPassword = "Admin123!$";

    // Una base en memoria por contexto. Mientras estas conexiones esten abiertas, la base existe.
    private readonly string _authDb = $"DataSource=auth-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly string _appDb = $"DataSource=app-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _authKeepAlive;
    private readonly SqliteConnection _appKeepAlive;

    public CustomWebApplicationFactory()
    {
        _authKeepAlive = new SqliteConnection(_authDb);
        _authKeepAlive.Open();
        _appKeepAlive = new SqliteConnection(_appDb);
        _appKeepAlive.Open();

        // Program.cs exige estos valores antes de construir la app (US-01 y US-21 del TP3).
        Environment.SetEnvironmentVariable("ConnectionStrings__Dsw2025TpiEntities", "se-reemplaza-por-sqlite");
        Environment.SetEnvironmentVariable("Jwt__Key", "clave-solo-para-pruebas-de-integracion-tp4-2026");
        Environment.SetEnvironmentVariable("SeedAdmin__Username", AdminUsername);
        Environment.SetEnvironmentVariable("SeedAdmin__Email", AdminEmail);
        Environment.SetEnvironmentVariable("SeedAdmin__Password", AdminPassword);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Sacar la configuracion de SQL Server de los dos contextos (EF Core 9 registra las dos piezas)
            services.RemoveAll<DbContextOptions<AuthenticateContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AuthenticateContext>>();
            services.RemoveAll<DbContextOptions<Dsw2025TpiContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<Dsw2025TpiContext>>();

            // Y registrarlos de nuevo apuntando a SQLite en memoria
            services.AddDbContext<AuthenticateContext>(o => o.UseSqlite(_authDb));
            services.AddDbContext<Dsw2025TpiContext>(o => o.UseSqlite(_appDb));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _authKeepAlive.Dispose();
            _appKeepAlive.Dispose();
        }
    }
}

// Todas las clases de esta coleccion comparten UNA sola API y corren una despues de otra.
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestsCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
