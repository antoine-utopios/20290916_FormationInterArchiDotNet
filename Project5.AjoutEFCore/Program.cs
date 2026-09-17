using Project5.AjoutEFCore.Data;
using Project5.AjoutEFCore.Models;
using Project5.AjoutEFCore.Mappers;
using Project5.AjoutEFCore.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
if (Environment.GetEnvironmentVariable("PROFILE") == "PROD")
{
    // builder.Services.AddDbContext<ApplicationDbContext>(options =>
    // {
    //     // Autre type de connexion ici...
    // });
} else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("SQLite"));
    });
    builder.Services.AddScoped<DogMapper>();
    builder.Services.AddScoped<IService<DogResponse, DogRequest, int>, DogService>();
    builder.Services.AddScoped<IRepository<Dog, int>, DogRepository>();
}

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
