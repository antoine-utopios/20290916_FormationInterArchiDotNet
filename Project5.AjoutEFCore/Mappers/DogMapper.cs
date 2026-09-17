using Project5.AjoutEFCore.Models;

namespace Project5.AjoutEFCore.Mappers;

public class DogMapper
{
    public Dog ToEntity(DogRequest request)
    {
        return new Dog()
        {
            Name = request.Name,
            Breed = (DogBreed) Enum.Parse(typeof(DogBreed), request.Breed.ToUpper()),
            Age = request.Age
        };
    }

    public DogResponse ToResponse(Dog entity)
    {
        return new DogResponse(
            entity.DogId, 
            entity.Name,
            entity.Breed.ToString(),
            entity.Age
        );
    }
}