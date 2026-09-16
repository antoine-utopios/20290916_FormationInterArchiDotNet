using Project3.WebAPIControllers.Models;

public class FakeDB
{
    private List<Client> clientLists;
    private int clientCounter;

    public FakeDB()
    {
        clientLists = new()
        {
          new Client() { Id = ++clientCounter, Firstname = "John", Lastname = "DUPONT", Email = "j.dupont@example.com"},
          new Client() { Id = ++clientCounter, Firstname = "Martha", Lastname = "DOE", Email = "m.doe@example.com"},
          new Client() { Id = ++clientCounter, Firstname = "Clark", Lastname = "SMITH", Email = "c.smith@example.com"}
        };
    }

    public IEnumerable<Client> GetAll()
    {
        return clientLists.AsEnumerable();
    }

    public Client? GetById(int clientId)
    {
        return clientLists.FirstOrDefault(x => x.Id == clientId);
    }

    public Client Save(Client newClient)
    {
        var clientAAjouter = new Client()
        {
            Id = ++clientCounter,
            Firstname = newClient.Firstname,
            Lastname = newClient.Lastname,
            Email = newClient.Email
        };

        clientLists.Add(clientAAjouter);

        return clientAAjouter;
    }
}