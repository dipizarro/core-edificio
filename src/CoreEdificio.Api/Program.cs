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

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddEndpointsApiExplorer();
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

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

/********** DI registrations **********/
builder.Services.AddScoped<ICommunityRepository, CommunityRepository>();
builder.Services.AddScoped<IUnitRepository, UnitRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IBillingPeriodRepository, BillingPeriodRepository>();
builder.Services.AddScoped<IUnitChargeRepository, UnitChargeRepository>();
builder.Services.AddScoped<IUnitReadRepository, UnitReadRepository>();
builder.Services.AddScoped<IChargeRepository, ChargeRepository>();
builder.Services.AddScoped<IFacilityBlockRepository, FacilityBlockRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IBillingReadRepository, BillingReadRepository>();
builder.Services.AddScoped<UserProvisioningService>();

builder.Services.AddSingleton<IAuthorizationHandler, CommunityScopeHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, UnitScopeHandler>();


builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<PaymentsService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CommunityService>();
builder.Services.AddScoped<UnitService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IStatementPdfGenerator, QuestStatementPdfGenerator>();

/********** AUTH **********/
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

var jwtKey = builder.Configuration["Jwt:Key"]!;
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.CommunityScope, policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new CommunityScopeRequirement()));

    options.AddPolicy(AuthPolicies.UnitScope, policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new UnitScopeRequirement()));
});


builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    /********** SEEDER de usuarios y roles **********/
    using var scope = app.Services.CreateScope();

    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await IdentitySeeder.SeedAsync(users, roles, db);
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("DevCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


app.Run();
