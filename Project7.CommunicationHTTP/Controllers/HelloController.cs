using Microsoft.AspNetCore.Mvc;
using Project7.CommunicationHTTP.Services;

namespace Project7.CommunicationHTTP.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HelloController : ControllerBase
{
    private TodoItemService _service;

    public HelloController(TodoItemService service)
    {
        _service = service;   
    }

    [HttpGet]
    public async Task<ActionResult> SayHi()
    {
        var results = await _service.GetAllTodosFromAPI(); 

        if (results is not null)
        {
            System.Console.WriteLine($"On a récupéré à la volée {results.Count} todo(s)!");
        } else
        {
            System.Console.WriteLine("Pas de todos :/");
        }

        return Ok(new { Message = "Hello world!"});
    }
}