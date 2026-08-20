using Microsoft.EntityFrameworkCore;
using Okozukai.Api.Middlewares;
using Okozukai.Application;
using Okozukai.Infrastructure;
using Okozukai.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai");

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.Services.ApplyDatabaseMigrations();

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("--> Environment is Development. Seeding data...");
    app.Services.SeedDevelopmentData();
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
