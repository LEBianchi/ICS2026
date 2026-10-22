using Dsw2025Tpi.Api.NewFolder;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Data.Helper;
using Dsw2025Tpi.Data.Repositories;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

namespace Dsw2025Tpi.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("Dsw2025TpiEntities");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta la cadena de conexion ConnectionStrings:Dsw2025TpiEntities. " +
                "En desarrollo: dotnet user-secrets. En Azure o Docker: variable ConnectionStrings__Dsw2025TpiEntities.");
        }

        // Add services to the container.
        builder.Services.AddLogging(config =>
        {
            config.ClearProviders(); //para limpiar prov por defecto
            config.AddConsole();

            var path = builder.Configuration.GetValue<string>("LogPath");
            if (!string.IsNullOrEmpty(path))
            {
                config.AddFile(path);
            }
        });

        builder.Services.AddControllers().AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Desarollo de software TPI",
                Version = "v1",
            });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Ingrese el token",
                Type = SecuritySchemeType.ApiKey
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
             });
        });

        builder.Services.AddHealthChecks();

        builder.Services.AddDbContext<AuthenticateContext>(options =>
        {
            options.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure());
        });

        // --- AQUÍ ESTÁ EL CAMBIO ---
        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password = new PasswordOptions
            {
                RequiredLength = 8
            };
        })
        .AddEntityFrameworkStores<AuthenticateContext>()
        .AddDefaultTokenProviders()
        .AddErrorDescriber<SpanishIdentityErrorDescriber>();

        var jwtConfig = builder.Configuration.GetSection("Jwt");
        var keyText = jwtConfig["Key"];
        if (string.IsNullOrWhiteSpace(keyText) || Encoding.UTF8.GetByteCount(keyText) < 32)
        {
            throw new InvalidOperationException(
                "Falta la clave JWT (Jwt:Key) o tiene menos de 32 caracteres. " +
                "En desarrollo: dotnet user-secrets. En Azure o Docker: variable Jwt__Key.");
        }
        var key = Encoding.UTF8.GetBytes(keyText);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtConfig["Issuer"],
                    ValidAudience = jwtConfig["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });

        builder.Services.AddDbContext<Dsw2025TpiContext>(options =>
        {
            options.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure());
        });

        builder.Services.AddSingleton<JwtTokenService>();
        builder.Services.AddAuthorization();

        builder.Services.AddScoped<IRepository, EfRepository>();
        builder.Services.AddScoped<ProductsManagementService>();
        builder.Services.AddScoped<OrdersManagementService>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend",
                policy =>
                {
                    policy.WithOrigins("http://localhost:5173")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
        });

        var app = builder.Build();

        // Crea las tablas, los roles, el administrador inicial y los clientes de ejemplo
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();

            try
            {
                // Crea las tablas si no existen y aplica las migraciones pendientes
                await services.GetRequiredService<AuthenticateContext>().Database.MigrateAsync();
                var context = services.GetRequiredService<Dsw2025TpiContext>();
                await context.Database.MigrateAsync();
                logger.LogInformation("Migraciones aplicadas en los dos contextos.");

                var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                string adminRole = "Admin";
                string userRole = "User";

                // Crear roles si no existen
                if (!await roleManager.RoleExistsAsync(adminRole))
                {
                    await roleManager.CreateAsync(new IdentityRole(adminRole));
                    Console.WriteLine($"Rol '{adminRole}' creado.");
                }
                if (!await roleManager.RoleExistsAsync(userRole))
                {
                    await roleManager.CreateAsync(new IdentityRole(userRole));
                    Console.WriteLine($"Rol '{userRole}' creado.");
                }

                // Administrador inicial desde la configuracion (User Secrets o variables de entorno)
                var adminConfig = app.Configuration.GetSection("SeedAdmin");
                var adminUsername = adminConfig["Username"];
                var adminEmail = adminConfig["Email"];
                var adminPassword = adminConfig["Password"];

                if (string.IsNullOrWhiteSpace(adminUsername) ||
                    string.IsNullOrWhiteSpace(adminEmail) ||
                    string.IsNullOrWhiteSpace(adminPassword))
                {
                    throw new InvalidOperationException(
                        "Falta la configuracion del administrador inicial: SeedAdmin:Username, SeedAdmin:Email y SeedAdmin:Password. " +
                        "En desarrollo: dotnet user-secrets. En Azure o Docker: SeedAdmin__Username, SeedAdmin__Email y SeedAdmin__Password.");
                }

                var adminUser = await userManager.FindByNameAsync(adminUsername);
                if (adminUser == null)
                {
                    adminUser = new IdentityUser
                    {
                        UserName = adminUsername,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };
                    var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                    if (!createResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "No se pudo crear el administrador inicial: " +
                            string.Join(" ", createResult.Errors.Select(e => e.Description)));
                    }
                    logger.LogInformation("Administrador inicial {Username} creado.", adminUsername);
                }
                else if (!string.Equals(adminUser.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Ya existe un usuario '{adminUsername}' con otro email. No se le asigna el rol de administrador.");
                }

                if (!await userManager.IsInRoleAsync(adminUser, adminRole))
                {
                    await userManager.AddToRoleAsync(adminUser, adminRole);
                    logger.LogInformation("Administrador inicial {Username} asignado al rol {Role}.", adminUsername, adminRole);
                }

                context.Seedwork<Customer>("customers.json");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Error al inicializar la base de datos. La API no va a arrancar.");
                throw;
            }
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowFrontend");

        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.MapHealthChecks("/healthcheck");

        app.Run();
    }
}