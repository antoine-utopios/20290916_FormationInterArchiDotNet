using Project3.WebAPIControllers.Models;

namespace Project3.WebAPIControllers.Mappers;

public class ClientMapper
{
    public ClientWithFullNameResponse ToClientWithFullNameResponse(Client client)
    {
        return new ClientWithFullNameResponse(client.Id, $"{client.Firstname} {client.Lastname}");
    }

    public Client FromClientCreationRequest(ClientCreationRequest dto)
    {
        return new Client()
        {
            Firstname = dto.Firstname,
            Lastname = dto.Lastname,
            Email = dto.Email
        };
    }


}