using Microsoft.AspNetCore.Mvc;
using Project3.WebAPIControllers.Mappers;
using Project3.WebAPIControllers.Models;

namespace Project3.WebAPIControllers.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ClientController : ControllerBase
{


    private ClientMapper clientMapper;
    private FakeDB context;

    public ClientController(ClientMapper clientMapper, FakeDB context)
    {
        this.clientMapper = clientMapper;
        this.context = context;


    }

    [HttpGet(Name = "GetAllClients")]
    public ActionResult<List<Client>> GetAllClients()
    {
        var clientListVersionDTOs = context.GetAll()
        .Select(clientMapper.ToClientWithFullNameResponse)
        .ToList();

        return Ok(clientListVersionDTOs);
    }

    [HttpGet("{clientId:int}", Name = "GetClientById")]
    public ActionResult<Client?> GetClientById(int clientId)
    {
        var clientFound = context.GetById(clientId);

        if (clientFound is null) return NotFound();
        else {
            return Ok(clientMapper.ToClientWithFullNameResponse(clientFound));
        }
    }

    [HttpPost(Name = "SaveNewClient")]
    public ActionResult<Client?> SaveNewClient(ClientCreationRequest requestDto)
    {
        var clientToCreate = clientMapper.FromClientCreationRequest(requestDto);

        var clientCreated = context.Save(clientToCreate);

        return Created($"/api/v1/client/{clientCreated.Id}", clientCreated);
    }
}