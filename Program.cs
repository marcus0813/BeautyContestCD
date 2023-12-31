using API.Data;
using API.Entities;
using API.Extensions;
using API.Middleware;
using API.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var logger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.File("./Logs/TransactionLogs.json", rollingInterval: RollingInterval.Day,
                           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties} {NewLine}")
                .CreateLogger();

builder.Host.UseSerilog(logger);


var app = builder.Build();


// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionMiddleware>();

// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
// }

app.UseHttpsRedirection();
app.UseCors(builder => builder.AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithOrigins("https://localhost:4200", "https://localhost:4200/*", "https://beautycontestvotingsystem.azurewebsites.net/", "https://beautycontestvotingsystem.azurewebsites.net/*")
        );

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<PresenceHub>("hubs/presence");
app.MapFallbackToController("Index", "Fallback");

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;

var context = services.GetRequiredService<DataContext>();
var userManager = services.GetRequiredService<UserManager<AppUser>>();
var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
await context.Database.MigrateAsync();
await Seed.SeedUsers(userManager, roleManager);

app.UseSerilogRequestLogging();

app.Run();
