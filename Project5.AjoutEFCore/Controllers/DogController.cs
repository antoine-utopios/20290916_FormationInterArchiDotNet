using Microsoft.AspNetCore.Mvc;
using Project5.AjoutEFCore.Services;
using Project5.AjoutEFCore.Models;

namespace Project5.AjoutEFCore.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DogController : ControllerBase
{
    private IService<DogResponse, DogRequest, int> _service;

    public DogController(IService<DogResponse, DogRequest, int> service)
    {
        _service = service;
    }

    [HttpGet]
    public ActionResult<List<DogResponse>> GetAll()
    {
        return Ok(_service.GetAll());
    }

    [HttpGet("{dogId:int}")]
    public ActionResult<DogResponse?> GetById(int dogId)
    {
        var dogFound = _service.GetById(dogId);
        if (dogFound is null) return NotFound();
        else return Ok(dogFound);
    }

    [HttpPost]
    public ActionResult<Dog?> AddDog(DogRequest request)
    {
        var dogAdded = _service.Add(request);
        if (dogAdded is null) return BadRequest();
        return Created($"api/v1/{dogAdded.DogId}", dogAdded);
    }
}