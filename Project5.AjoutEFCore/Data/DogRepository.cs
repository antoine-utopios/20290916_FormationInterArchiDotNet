using Project5.AjoutEFCore.Models;

namespace Project5.AjoutEFCore.Data;

public class DogRepository : IRepository<Dog, int>
{
    private ApplicationDbContext _context;

    public DogRepository(ApplicationDbContext context)
    {
        _context = context;
    }
       public IEnumerable<Dog> GetAll()
    {
        return _context.Dogs.AsEnumerable();
    }

    public Dog? GetById(int dogId)
    {
        return _context.Dogs.FirstOrDefault(x => x.DogId == dogId);
    }

    public Dog Add(Dog newDog)
    {
        _context.Dogs.Add(newDog);
        _context.SaveChanges();
        return newDog;
    }
}