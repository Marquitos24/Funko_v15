using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Funkos.Cache;
using Funkos.DbContext;
using Funkos.GraphQL.Mutations;
using Funkos.GraphQL.Queries;
using Funkos.GraphQL.Subscriptions;
using Funkos.GraphQL.Types;
using Funkos.Infrastructure.Storage;
using Funkos.Middleware;
using Funkos.Models;
using Funkos.Mongo;
using Funkos.RealTime.Notificaciones;
using Funkos.Repositories;
using Funkos.Repositories.Categoria;
using Funkos.Repositories.Proveedor;
using Funkos.Service;
using Funkos.Service.Categoria;
using Funkos.Service.Decorator;
using Funkos.Service.Proveedor;
using Funkos.Service.Storage;
using Funkos.Validations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using StackExchange.Redis;
using RateLimitPartition = System.Threading.RateLimiting.RateLimitPartition;
using Role = Funkos.Models.Role;

namespace Funkos.Infrastructure;

public static class ApplicationInfraestructure
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        #region Repositorios y Servicios CRUD
        services.AddScoped<IFunkoRepository, FunkoRepository>();
        services.AddScoped<ICategoriaRepository, CategoriaRepository>();
        services.AddScoped<IProveedorRepository, ProveedorRepository>();
        
        services.AddScoped<FunkoService>();
        services.AddScoped<ProveedorService>();
        services.AddScoped<CategoriaService>();
        #endregion

        #region VALIDACIONES (FluentValidation)
        // Registrar los validators --> NO ejecuta la validación por sí solo
        services.AddValidatorsFromAssemblyContaining<ProveedorUpdateValidation>();
        // Ejecución automática vía action filter --> Paquete FluentValidation .AspNetCore
        // Requiere llamarse DESPUÉS de AddControllers() = Solo funciona en MVC/controllers
        //builder.Services.AddFluentValidationAutoValidation();
        #endregion

        #region Decorator
        // Decorator como servicio que recibirá el Controller
        services.AddScoped<IFunkoService>(sp => new CachedFunkoServiceDecorator(sp.GetRequiredService<FunkoService>(), sp.GetRequiredService<ICacheService>(), sp.GetRequiredService<ILogger<CachedFunkoServiceDecorator>>()));
        services.AddScoped<ICategoriaService>(sp => new CachedCategoriaServiceDecorator(sp.GetRequiredService<CategoriaService>(), sp.GetRequiredService<ICacheService>(), sp.GetRequiredService<ILogger<CachedCategoriaServiceDecorator>>()));
        services.AddScoped<IProveedorService>(sp => new CachedProveedorServiceDecorator(sp.GetRequiredService<ProveedorService>(), sp.GetRequiredService<ICacheService>(), sp.GetRequiredService<ILogger<CachedProveedorServiceDecorator>>()));
        #endregion

        #region POSTGRESQL
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        // Conectamos desde appsettings
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        #endregion

        #region MONGO
        // MongoClient es thread-safe ==> se Reutiliza durante toda la app. MongoDbContext solo tiene referencias a la BD y a las colecciones.
        // No tiene el comportamiento de seguimiento de entidades de un DbContext de EF Core.
        var settings = configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()!;
        // Registrar configuración
        services.AddSingleton(settings);
        // Crear una única instancia de MongoClient
        services.AddSingleton<IMongoClient>(_ => new MongoClient(settings.ConnectionString));
        // Registrar contexto
        services.AddSingleton<MongoDbContext>();
        
        #endregion

        #region REDIS - CACHE
        //IConnectionMultiplexer ==> Objeto principal de StackExchange.Redis que gestiona las conexiones con el servidor Redis.
        // Desde él se puede obtener una BD Redis
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(
            configuration["Redis:ConnectionString"]!));

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis:ConnectionString"];
        });

        #endregion

        #region IMAGES
        services.AddScoped<IStorageService, FileSystemStorageService>();
        services.Configure<StorageSettings>(configuration.GetSection("Storage"));

        // Configurar límite de formularios multipart
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
        });
        #endregion

        #region IDENTITY Y JWT
        // ++++++++ IDENTITY Y JWT (SIN VISTAS NI COOKIES DE UI) ++++++
        // AddIdentityCore: Identity sin UI; roles y signInManager se añaden a mano
       services.AddIdentityCore<User>(options =>
            {
                // --- CONTRASEÑA ---
                options.Password.RequireDigit = true;           // Exige al menos un número (0-9).
                options.Password.RequireLowercase = true;       // Exige al menos una minúscula (a-z).
                options.Password.RequireUppercase = true;       // Exige al menos una mayúscula (A-Z).
                options.Password.RequireNonAlphanumeric = true; // Exige al menos un carácter especial (!, ?, @, #...).
                options.Password.RequiredLength = 8;            // Establece el mínimo de caracteres (8)

                // --- USUARIO ---
                options.User.RequireUniqueEmail = true;         // Email único

                // --- CUENTA ---
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); // Tiempo de bloqueo (15 minutos)
                options.Lockout.MaxFailedAccessAttempts = 5;    // Intentos fallidos permitidos antes del bloqueo (5)
                
                options.ClaimsIdentity.UserIdClaimType = JwtRegisteredClaimNames.Sub;
            })
            .AddRoles<Role>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Forzar JWT Bearer como esquema global evitando conflictos con cookies
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;                 //
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;     //
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;        //
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;           //
            })
            .AddJwtBearer(options =>
            {
                // Desactivado el mapeo automático de claims
                options.MapInboundClaims = false;
                
                #region Validacion del Token

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Validar que el token haya sido firmado con nuestra clave secreta
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret no configurado"))),
                    
                    // Validar quién emite el token (Issuer)
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    
                    // Validar para quién va dirigido el token (Audience)
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                   
                    //Validar que el token no haya expirado
                    ValidateLifetime = true,
                    
                    // Evita desfases horarios (da un margen de unos pocos segundos de tolerancia)
                    ClockSkew = TimeSpan.Zero,
                    
                    //Indicamos qué claim del JWT representa los roles
                    RoleClaimType = "role"
                };

                #endregion
            });
        #endregion

        #region AUTORIZACIONES
        // Central para gestionar las autorizacioness 
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(
                new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build())
            .AddPolicy("RequireAdmin", policy =>
                policy.RequireAssertion(context =>
                        context.User.IsInRole("ADMIN") && context.User.HasClaim("department", "IT")))
                
            .AddPolicy("RequireUser", policy =>
                policy.RequireRole("USER", "ADMIN"));
        
        //Futuras actualizaciones:
            // - Añadir Requirements y handlers personalizados
        #endregion
        
        #region SignalR y JWT
        // Registrar SignalR + Mapearlo con autorizacion + Cache.
        services.AddSignalR().AddHubOptions<NotificacionesHub>(options =>
        {
            options.EnableDetailedErrors = true;  // Solo en desarrollo
        })
            // Junto a Redis --> Redis Backplane
        .AddStackExchangeRedis("localhost:6379", options =>
        {
            options.Configuration.ChannelPrefix = "FunkosApp";
        });
        
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        // SignalR admite también el token por query string
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        // Solo extraer el token de la query si la petición va dirigida a un Hub de SignalR
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
        
        #endregion

        #region CORS
        services.AddCors(options =>
        {
            options.AddPolicy("Clientes", policy =>
                policy.WithOrigins(
                        "http://localhost:5175", 
                        "https://localhost:5175", 
                        "http://localhost:63342"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());  // ¡Imprescindible para SignalR!
        });
        #endregion

        #region RateLimit
        // Añade los servicios de limitación de tasa (Rate Limiting) al contenedor de dependencias de la aplicación
        services.AddRateLimiter(options =>{

            // Define una política de control de tráfico llamada "api" (pensada para usuarios generales/IPs)
            options.AddPolicy("api", context =>
            {
                // Obtiene la dirección IP del cliente que hace la petición; si no se puede determinar, usa "unknown"
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                
                // Crea una partición de límite de tasa basada en la IP utilizando una ventana fija (Fixed Window)
                return RateLimitPartition.GetFixedWindowLimiter(
                    ip,
                    partition => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,                                    // Máximo de 100 peticiones permitidas por ventana de tiempo
                        Window = TimeSpan.FromMinutes(1),                     // Duración de la ventana de tiempo (1 minuto)
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst // Si hay cola, las peticiones más antiguas se procesan primero
                    });
            });

            // Define otra política llamada "premium" (enfocada a usuarios autenticados/identificados por su token)
            options.AddPolicy("premium", context =>
            {
                // Busca el claim "sub" (ID de usuario) en el token JWT; si no está logueado, se le trata como "anonymous"
                var userId = context.User.FindFirst("sub")?.Value ?? "anonymous";
                
                // Crea una partición de límite de tasa usando el ID del usuario con una ventana fija más permisiva
                return RateLimitPartition.GetFixedWindowLimiter(
                    userId,
                    partition => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 1000,                  // Máximo de 1000 peticiones permitidas por ventana
                        Window = TimeSpan.FromMinutes(1)     // Duración de la ventana de tiempo (1 minuto)
                    });
            });

            // Configura qué ocurre globalmente cuando un usuario supera el límite permitido (Exceso de peticiones)
            options.OnRejected = async (context, cancellationToken) =>
            {
                // Cambia el código de estado HTTP de la respuesta a 429 Too Many Requests (Demasiadas solicitudes)
                context.HttpContext.Response.StatusCode = 429;
                
                // Escribe una respuesta personalizada en formato JSON informando del error al cliente
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Demasiadas peticiones. Intenta de nuevo más tarde.",
                    
                    // Intenta extraer del sistema cuánto tiempo exacto debe esperar el cliente para reintentar
                    retryAfter = context.Lease.TryGetMetadata(
                        MetadataName.RetryAfter, out var retryAfter)
                        ? retryAfter.TotalSeconds   // Si existe el dato, devuelve los segundos de espera
                        : (double?)null             // Si no, devuelve nulo
                }, cancellationToken);
            };
        });
        
        // Para el Middleware
        services.AddSingleton<RateLimitingState>(); // particiones (IP + verbo + ruta)
        #endregion

        #region GRAPHQL con HotChocolate
        services
            .AddGraphQLServer()
            .AddQueryType<TiendaQuery>()
            .AddMutationType<TiendaMutation>()
            .AddType<FunkoType>()
            .AddType<CategoriaType>()
            .AddType<ProveedorType>()
            .AddSubscriptionType<Subscription>()
            .AddType<UploadType>()
            .AddInMemorySubscriptions()
            .AddAuthorization()
            .AddFiltering()
            .AddSorting()
            // Un solo contrato: los types se construyen sobre losDTOs y
            // RemoveUnreachableTypes elimina del esquema cualquier tipo registrado al que ninguna operación pueda llegar.
            // Así la introspección nunca vuelve a prometer campos que no se pueden consultar.
            .ModifyOptions(options => options.RemoveUnreachableTypes = true)
            /*
             * .ModifyRequestOptions(opt =>
            {
                opt.IncludeExceptionDetails = IWebHostEnvironment.IsDevelopment();
            })
            if (!environment.IsDevelopment())
            {
                builder.DisableIntrospection();
            }

             */
            .AddProjections();
        

        #endregion
        
        return services;
    }
}