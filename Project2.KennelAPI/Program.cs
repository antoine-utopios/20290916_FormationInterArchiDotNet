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

int dogIdCount = 0;

var dogList = new List<Dog>()
{
    new Dog { DogId = ++dogIdCount, Name = "Bernie", Breed = "Labrador", Age = 2 },  
    new Dog { DogId = ++dogIdCount, Name = "Saucisse", Breed = "Tackel", Age = 4 },  
    new Dog { DogId = ++dogIdCount, Name = "Rex", Breed = "German Shepard", Age = 7 }  
};

// GET: /api/v1/dogs
app.MapGet("/api/v1/dogs", () =>
{
    return Results.Ok(dogList);
})
.WithName("GetAllDogs");

// GET: /api/v1/dogs/:dogId
app.MapGet("/api/v1/dogs/{dogId:int}", (int dogId) =>
{
    var dogFound = dogList.FirstOrDefault(x => x.DogId == dogId);
    if (dogFound is null) return Results.NotFound(new { Message = $"Pas de chien trouvé pour l'ID: {dogId}!"});
    else return Results.Ok(dogFound);
})
.WithName("GetDogById");

// POST: /api/v1/dogs
app.MapPost("/api/v1/dogs", (Dog newDog) =>
{
    var newDogToAdd = new Dog()
    {
        DogId = ++dogIdCount,
        Name = newDog.Name,
        Breed = newDog.Breed,
        Age = newDog.Age
    };

    dogList.Add(newDogToAdd);

    return Results.Created($"/api/v1/dogs/{newDogToAdd.DogId}", newDogToAdd);
})
.WithName("PostNewDog");

// PATCH: /api/v1/dogs
app.MapPatch("/api/v1/dogs", (Dog newInfos) =>
{
    if (newInfos.DogId == 0) return Results.NotFound(new { Message = $"Pas de chien trouvé pour l'ID: {newInfos.DogId}!"});

    var dogFound = dogList.FirstOrDefault(x => x.DogId == newInfos.DogId);
    if (dogFound is null) return Results.NotFound(new { Message = $"Pas de chien trouvé pour l'ID: {newInfos.DogId}!"});
    else
    {
        if (!String.IsNullOrEmpty(newInfos.Name) && dogFound.Name != newInfos.Name) dogFound.Name = newInfos.Name;
        if (!String.IsNullOrEmpty(newInfos.Breed) && dogFound.Breed != newInfos.Breed) dogFound.Breed = newInfos.Breed;
        if (newInfos.Age != 0 && dogFound.Age != newInfos.Age) dogFound.Age = newInfos.Age;

        return Results.NoContent();
    }
})
.WithName("EditDogWithId");

// DELETE: /api/v1/dogs/:dogId
app.MapDelete("/api/v1/dogs/{dogId:int}", (int dogId) =>
{
    if (dogId == 0) return Results.NotFound(new { Message = $"Pas de chien trouvé pour l'ID: {dogId}!"});

    var dogFound = dogList.FirstOrDefault(x => x.DogId == dogId);
    if (dogFound is null) return Results.NotFound(new { Message = $"Pas de chien trouvé pour l'ID: {dogId}!"});
    else
    {
        dogList.Remove(dogFound);
        return Results.NoContent();
    }
})
.WithName("DeleteDogById");

app.Run();
