using Textinord.Trame.Application.DependencyInjection;
using Textinord.Trame.Infrastructure.DependencyInjection;
using Textinord.Trame.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTrameApplication();
builder.Services.AddTrameInfrastructure();
builder.Services.AddHostedService<SimulationCommandesWorker>();

var host = builder.Build();
host.Run();
