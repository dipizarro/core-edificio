using CoreEdificio.Api.Middlewares;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Application.Services;
using CoreEdificio.Infrastructure.Persistence;
using CoreEdificio.Infrastructure.Repositories;
using CoreEdificio.Infrastructure.Repositories.Billing;
using CoreEdificio.Infrastructure.Repositories.Payments;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

/********** DI registrations **********/
builder.Services.AddScoped<ICommunityRepository, CommunityRepository>();
builder.Services.AddScoped<IUnitRepository, UnitRepository>();
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IBillingPeriodRepository, BillingPeriodRepository>();
builder.Services.AddScoped<IUnitChargeRepository, UnitChargeRepository>();
builder.Services.AddScoped<IUnitReadRepository, UnitReadRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IBillingReadRepository, BillingReadRepository>();

builder.Services.AddScoped<PaymentsService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CommunityService>();
builder.Services.AddScoped<UnitService>();



builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseMiddleware<ExceptionMiddleware>();
app.MapControllers();

app.Run();
