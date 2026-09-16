using Project3.WebAPIControllers.Models;

namespace Project3.WebAPIControllers.Mappers;

public class ClientMapper
{
    public ClientWithFullNameResponse ToClientWithFullNameResponse(Client client)
    {
        return new ClientWithFullNameResponse(client.Id, $"{client.Firstname} {client.Lastname}");
    }

    public Client FromClientWithFullNameResponse(ClientWithFullNameResponse dto)
    {
        var fullNameSplit = dto.FullName.Split(" ");

        return new Client()
        {
            Id = dto.ClientId,
            Firstname = fullNameSplit[0],
            Lastname = fullNameSplit[1]
        };
    }


}