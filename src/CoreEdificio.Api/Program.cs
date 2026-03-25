using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Middlewares;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Application.Interfaces.Identity;
using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Application.Services;
using CoreEdificio.Infrastructure.Auth;
using CoreEdificio.Infrastructure.Identity;
using CoreEdificio.Infrastructure.Persistence;
using CoreEdificio.Infrastructure.Repositories;
using CoreEdificio.Infrastructure.Repositories.Billing;
using CoreEdificio.Infrastructure.Repositories.Payments;
using CoreEdificio.Infrastructure.Services.Billing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// 1. Configuraciones iniciales
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 2. Configuración de CORS basada en appsettings
var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Documentación Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CoreEdificio API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } } , Array.Empty<string>() } });
});

// 4. Base de Datos
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("Default") 
        ?? throw new InvalidOperationException("Connection string 'Default' not found.")));

// TODO: Refactor -> Mover a CoreEdificio.Infrastructure.DependencyInjection.cs (services.AddInfrastructure())
RegisterInfrastructureServices(builder.Services);

// TODO: Refactor -> Mover a CoreEdificio.Application.DependencyInjection.cs (services.AddApplication())
RegisterApplicationServices(builder.Services);

// 5. Autenticación e Identidad
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Se recomienda obtener estas reglas de configuración (appsettings)
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

var jwtKey = configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing in configuration");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// 6. Autorización
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.CommunityScope, policy => policy.RequireAuthenticatedUser().AddRequirements(new CommunityScopeRequirement()));
    options.AddPolicy(AuthPolicies.UnitScope, policy => policy.RequireAuthenticatedUser().AddRequirements(new UnitScopeRequirement()));
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Seeder
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    await IdentitySeeder.SeedAsync(
        services.GetRequiredService<UserManager<ApplicationUser>>(),
        services.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
        services.GetRequiredService<AppDbContext>(),
        services.GetService<ILoggerFactory>()?.CreateLogger("IdentitySeeder")
    );
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("DefaultCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// --- Métodos locales para organizar la DI (Paso intermedio hacia Clean Architecture pura) ---
void RegisterInfrastructureServices(IServiceCollection services)
{
    services.AddScoped<ICommunityRepository, CommunityRepository>();
    services.AddScoped<IUnitRepository, UnitRepository>();
    services.AddScoped<IBookingRepository, BookingRepository>();
    services.AddScoped<IExpenseRepository, ExpenseRepository>();
    services.AddScoped<IBillingPeriodRepository, BillingPeriodRepository>();
    services.AddScoped<IUnitChargeRepository, UnitChargeRepository>();
    services.AddScoped<IUnitReadRepository, UnitReadRepository>();
    services.AddScoped<IChargeRepository, ChargeRepository>();
    services.AddScoped<IFacilityBlockRepository, FacilityBlockRepository>();
    services.AddScoped<IFacilityRepository, FacilityRepository>();
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddScoped<IPaymentRepository, PaymentRepository>();
    services.AddScoped<IBillingReadRepository, BillingReadRepository>();
    
    services.AddScoped<IIdentityService, IdentityService>();
    services.AddScoped<IStatementPdfGenerator, QuestStatementPdfGenerator>();
    services.AddScoped<JwtTokenService>();
    
    services.AddSingleton<IAuthorizationHandler, CommunityScopeHandler>();
    services.AddSingleton<IAuthorizationHandler, UnitScopeHandler>();
}

void RegisterApplicationServices(IServiceCollection services)
{
    services.AddScoped<UserProvisioningService>();
    services.AddScoped<PaymentsService>();
    services.AddScoped<BillingService>();
    services.AddScoped<CommunityService>();
    services.AddScoped<UnitService>();
    services.AddScoped<FacilityService>();
    services.AddScoped<BookingService>();
}
