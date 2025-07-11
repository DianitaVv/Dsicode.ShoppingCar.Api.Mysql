using AutoMapper;
using Dsicode.ShoppingCart.Api.Contract;
using Dsicode.ShoppingCart.Api.Services;
using Dsicode.ShoppingCart.Api;
using Dsicode.ShoppingCart.Api.Data;
using Dsicode.ShoppingCart.Api.Extensions;
using Dsicode.ShoppingCart.Api.Services;
using Dsicode.ShoppingCart.API.Utility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ✅ Configurar la base de datos - MySQL con esquemas separados
builder.Services.AddDbContext<AppDbContext>(option =>
{
    option.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 0)),
        mySqlOptions =>
        {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10, // ✅ Incrementar para Docker
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            mySqlOptions.CommandTimeout(120); // ✅ Incrementar timeout
            mySqlOptions.SchemaBehavior(MySqlSchemaBehavior.Ignore); // ✅ Para una sola DB
        }
    );
});

// ✅ Configurar AutoMapper
IMapper mapper = MappingConfig.RegisterMaps().CreateMapper();
builder.Services.AddSingleton(mapper);

// ✅ Registrar servicios
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICouponService, CouponService>();

// ✅ Agregar el interceptor al middleware
builder.Services.AddScoped<BackendApiAuthenticationHttpClientHandler>();
builder.Services.AddHttpContextAccessor();

// ✅ Configurar HttpClients para comunicación entre microservicios
builder.Services.AddHttpClient("Product", client =>
{
    var productUrl = builder.Configuration["ServiceUrls:ProductAPI"];
    client.BaseAddress = new Uri(productUrl);
    client.Timeout = TimeSpan.FromSeconds(30); // ✅ Timeout para Docker
})
.AddHttpMessageHandler<BackendApiAuthenticationHttpClientHandler>();

builder.Services.AddHttpClient("Coupon", client =>
{
    var couponUrl = builder.Configuration["ServiceUrls:CouponAPI"];
    client.BaseAddress = new Uri(couponUrl);
    client.Timeout = TimeSpan.FromSeconds(30); // ✅ Timeout para Docker
})
.AddHttpMessageHandler<BackendApiAuthenticationHttpClientHandler>();

// ✅ JWT Authentication Configuration
var secret = builder.Configuration["ApiSettings:JwtOptions:Secret"];
var issuer = builder.Configuration["ApiSettings:JwtOptions:Issuer"];
var audience = builder.Configuration["ApiSettings:JwtOptions:Audience"];

if (string.IsNullOrEmpty(secret))
{
    throw new InvalidOperationException("ApiSettings:JwtOptions:Secret no está configurado en appsettings.json");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // ✅ Importante para Docker
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero, // ✅ Para Docker
        RoleClaimType = "role"  // ✅ CRÍTICO: Para que funcionen los roles
    };
    options.MapInboundClaims = false;

    // ✅ Eventos para debugging en Docker
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"❌ JWT Authentication failed: {context.Exception.Message}");
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Append("IS-TOKEN-EXPIRED", "true");
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("✅ JWT Token validated successfully");

            // ✅ Debug: Ver todos los claims
            var claimsIdentity = context.Principal.Identity as ClaimsIdentity;
            Console.WriteLine("🔍 Claims en el token:");
            foreach (var claim in claimsIdentity.Claims)
            {
                Console.WriteLine($"  {claim.Type}: {claim.Value}");
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ✅ Configurar controladores con opciones para Docker
builder.Services.AddControllers().AddJsonOptions(opts =>
    opts.JsonSerializerOptions.ReferenceHandler =
    System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
);

builder.Services.AddEndpointsApiExplorer();

// ✅ CORS configuration para Docker
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ✅ Swagger configuration para Docker
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShoppingCart API",
        Version = "v1",
        Description = "API para gestión del carrito de compras"
    });

    option.AddSecurityDefinition(name: JwtBearerDefaults.AuthenticationScheme, securityScheme: new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter the Bearer Authorization string as following: `Bearer Generated-JWT-Token`",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// ✅ Configuración para todos los entornos (Docker)
app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ShoppingCart API V1");
    c.RoutePrefix = "swagger"; // ✅ Para que funcione con nginx
});

// ✅ Middleware para Docker
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ✅ Aplicar migraciones automáticamente con reintentos para Docker
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var maxRetries = 10;
    var delay = TimeSpan.FromSeconds(5);

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            Console.WriteLine($"🔄 CartDB: Intento {i + 1} - Aplicando migraciones...");

            // ✅ Verificar conexión primero
            await context.Database.CanConnectAsync();

            // ✅ Aplicar migraciones
            await context.Database.MigrateAsync();

            Console.WriteLine("✅ CartDB: Migraciones aplicadas correctamente");
            break; // ✅ Salir del loop si es exitoso
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CartDB: Error en intento {i + 1}: {ex.Message}");

            if (i == maxRetries - 1)
            {
                Console.WriteLine($"❌ CartDB: Falló después de {maxRetries} intentos");
                throw; // ✅ Re-lanzar excepción en el último intento
            }

            Console.WriteLine($"⏳ CartDB: Esperando {delay.TotalSeconds}s antes del siguiente intento...");
            await Task.Delay(delay);
        }
    }
}

Console.WriteLine("🚀 Microservicio de ShoppingCart iniciado correctamente");
app.Run();