using Textinord.Trame.Infrastructure;
using Textinord.Trame.Worker;
using Textinord.Trame.Worker.Outbox;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTrameInfrastructure(builder.Configuration);
builder.Services.AddTrameMessaging(builder.Configuration);

builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.Section));
builder.Services.AddScoped<OutboxRelay>();
builder.Services.AddHostedService<OutboxRelayService>();

var host = builder.Build();
await host.RunAsync();
