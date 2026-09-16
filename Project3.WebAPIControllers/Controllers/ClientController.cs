using Microsoft.AspNetCore.Mvc;
using Project3.WebAPIControllers.Models;

namespace Project3.WebAPIControllers.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ClientController : ControllerBase
{
    private List<Client> clientLists;
    private int clientCounter;

    public ClientController()
    {
        clientLists = new()
        {
          new Client() { Id = ++clientCounter, Firstname = "John", Lastname = "DUPONT", Email = "j.dupont@example.com"},
          new Client() { Id = ++clientCounter, Firstname = "Martha", Lastname = "DOE", Email = "m.doe@example.com"},
          new Client() { Id = ++clientCounter, Firstname = "Clark", Lastname = "SMITH", Email = "c.smith@example.com"}
        };
    }

    [HttpGet(Name = "GetAllClients")]
    public ActionResult<List<Client>> GetAllClients()
    {
        var clientListVersionDTOs = clientLists
        .Select(clientEnCoursIteration => new ClientWithFullNameResponse(clientEnCoursIteration.Id, $"{clientEnCoursIteration.Firstname} {clientEnCoursIteration.Lastname}"))
        .ToList();

        return Ok(clientListVersionDTOs);
    }

    [HttpGet("{clientId:int}", Name = "GetClientById")]
    public ActionResult<Client?> GetClientById(int clientId)
    {
        var clientFound = clientLists.FirstOrDefault(x => x.Id == clientId);

        if (clientFound is null) return NotFound();
        else {
            var clientFoundVersionDTO = new ClientWithFullNameResponse(clientFound.Id, $"{clientFound.Firstname} {clientFound.Lastname}");
            
            return Ok(clientFoundVersionDTO);
        }
    }
}