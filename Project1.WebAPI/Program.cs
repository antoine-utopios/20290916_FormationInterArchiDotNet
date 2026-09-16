var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select((index) =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// PARTIE CLIENTS 

// Pour la démonstration, on va utiliser une liste de clients en mémoire. Les clients sont des simples entités de type Record pour la facilité.
int clientIdCount = 0;

var clientList = new List<Client>()
{
    new Client(++clientIdCount, "John", "DUPONT"),
    new Client(++clientIdCount, "Martha", "SMITH"),
    new Client(++clientIdCount, "Jake", "SULLY")
};

app.MapGet("/clients", () =>
{
    // On peut imaginer récupérer les clients en base de données, les transformer à la volée pour obtenir une version propre vis à vis du dotNet. Via la sérialisation automatique perpétrée par Jackson, on va avoir un JSON en sortie de toute façon
    return clientList;
})
.WithName("GetClientList");

app.MapGet("/clients/{clientId:int}", (int clientId) =>
{
    var clientFound = clientList.FirstOrDefault(client => client.clientId == clientId);

    if (clientFound is not null) return Results.Ok(clientFound);
    else
    {
        var objectRetour = new { Message = "Pas de client trouvé avec cet ID!"};
        return Results.NotFound(objectRetour);
    }
})
.WithName("GetClientById");

app.MapPost("/clients", (Client newClient) =>
{
    var newClientToSave = newClient with { clientId = ++clientIdCount };
    clientList.Add(newClientToSave);

    return Results.Created($"/clients/{newClientToSave.clientId}", newClientToSave);
})
.WithName("AddNewClient");

app.MapDelete("/clients/{clientId:int}", (int clientId) =>
{
    var clientFound = clientList.FirstOrDefault(x => x.clientId == clientId);
    if (clientFound is null) return Results.NotFound();
    else
    {
        clientList.Remove(clientFound);
        return Results.NoContent();
    }
})
.WithName("DeleteClientById");

app.MapPatch("/clients", (Client newInfos) =>
{
    if (newInfos.clientId == 0) return Results.NotFound(new { Message = "Pas de client trouvé avec cet ID!"});

    var clientFound = clientList.FirstOrDefault(x => x.clientId == newInfos.clientId);
    if (clientFound is null) return Results.NotFound();
    else
    {
        
        if (newInfos.firstName is not null && newInfos.firstName != clientFound.firstName)
        {
            // Modification du prénom uniquement
            var clientWithNewFirstName = clientFound with { firstName = newInfos.firstName };
        }

        if (newInfos.lastName is not null && newInfos.lastName != clientFound.lastName)
        {
            // Modification du nom de famille uniquement
            var clientWithNewLastName = clientFound with { lastName = newInfos.lastName };
        }
        
        return Results.NoContent();
    }
})
.WithName("EditClientById");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record Client(int clientId, string firstName, string lastName);
