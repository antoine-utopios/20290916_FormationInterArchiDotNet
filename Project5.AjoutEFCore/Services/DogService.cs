using Project5.AjoutEFCore.Models;
using Project5.AjoutEFCore.Mappers;
using Project5.AjoutEFCore.Data;

namespace Project5.AjoutEFCore.Services;

public class DogService : IService<DogResponse, DogRequest, int>
{
    private DogMapper _mapper;
    private IRepository<Dog, int> _repo;

    public DogService(DogMapper mapper, IRepository<Dog, int> repo)
    {
        _mapper = mapper;
        _repo = repo;
    }

    public IEnumerable<DogResponse> GetAll()
    {
        return _repo.GetAll().Select(_mapper.ToResponse).AsEnumerable();
    }

    public DogResponse? GetById(int dogId)
    {
        var dogFound = _repo.GetById(dogId);
        if (dogFound is not null) return _mapper.ToResponse(dogFound);
        // else return default; => Pour retourner la valeur par défaut du type prévu
        else return null;
    }

    public DogResponse? Add(DogRequest request)
    {
        var dogEntity = _mapper.ToEntity(request);
        var dogAdded = _repo.Add(dogEntity);
        return _mapper.ToResponse(dogAdded);
    }
}