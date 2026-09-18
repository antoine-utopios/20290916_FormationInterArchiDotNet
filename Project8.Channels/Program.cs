using System.Threading.Channels;

#region Bases
// Pour communiquer via la RAM, on va utiliser le mécanisme des canaux fournis nativement par System.Threading.

// Si l'on veut mettre en place une protection bia une BackPressure, on doit utiliser un canal limité qui force le passage des données paquets par paquets
Channel<string> channelNonSecure = Channel.CreateUnbounded<string>();
Channel<string> channelWithBackpressure = Channel.CreateBounded<string>(1_000);

// A partir d'un canal, on peut extraire un écrivain, dans le but de pouvoir mettre des données dans le canal, une à une, potentiellement paquets par paquets
ChannelWriter<string> channelWriter = channelNonSecure.Writer;
await channelWriter.WriteAsync("Hello world!");

// On peut également extraire un lecteur dans le but de pouvoir créer une boucle qui va parcourir l'ensemble des messages et provoquer un comportement pour chaque message entrant
ChannelReader<string> channelReader = channelNonSecure.Reader;
await foreach (var message in channelReader.ReadAllAsync())
{
    System.Console.WriteLine(message);
}
#endregion

#region AvecOptions
var options = new BoundedChannelOptions(1_000)
{
    // Wait = pour attendre que la place se libère
    // DropOldest = Supprime l'ancien message pour faire de la place
    // DropNewest = On rejète ce qui arrive car plus de place disponible
    // DropWrite = On ne traite simplement pas le nouveau message
    FullMode = BoundedChannelFullMode.Wait, 
    SingleReader = true,
    SingleWriter = true
};

Channel<string> canalAvecOptions = Channel.CreateBounded<string>(options);

// A partir d'un canal, on peut extraire un écrivain, dans le but de pouvoir mettre des données dans le canal, une à une, potentiellement paquets par paquets
ChannelWriter<string> optionWriter = canalAvecOptions.Writer;
await optionWriter.WriteAsync("Hello world!");

// On peut également extraire un lecteur dans le but de pouvoir créer une boucle qui va parcourir l'ensemble des messages et provoquer un comportement pour chaque message entrant
ChannelReader<string> optionReader = canalAvecOptions.Reader;
await foreach (var message in optionReader.ReadAllAsync())
{
    System.Console.WriteLine(message);
}
#endregion

#region Multiples producteurs / consommateurs
var optionsMultiple = new BoundedChannelOptions(1_000)
{
    FullMode = BoundedChannelFullMode.Wait
};

Channel<string> canalConsoMultiples = Channel.CreateBounded<string>(optionsMultiple);

var producers = Enumerable.Range(0, 5).Select(async indexProducer =>
{
   for (int i = 0; i < 5; i++)
    {
        await canalConsoMultiples.Writer.WriteAsync($"Message du producer n°{indexProducer}: {i}");
    } 
});

var consumers = Enumerable.Range(0, 3).Select(async _ =>
{
    await foreach (var message in canalConsoMultiples.Reader.ReadAllAsync())
    {
        System.Console.WriteLine(message);
    }
});


await Task.WhenAll(consumers);
canalConsoMultiples.Writer.Complete();
await Task.WhenAll(consumers);

// List<Task> allTasks = new();
// allTasks.AddRange(producers);
// allTasks.AddRange(consumers);
#endregion

#region AvecFiltre

var entree = Channel.CreateUnbounded<CommandePerso>();
var sortie = Channel.CreateUnbounded<CommandePerso>();

var producteur = Task.Run(async () =>
{
    await entree.Writer.WriteAsync(new CommandePerso(1, "Banane", 5, false));
    await entree.Writer.WriteAsync(new CommandePerso(2, "Fraise", 3, true));
    await entree.Writer.WriteAsync(new CommandePerso(3, "Kiwi", 8, false));
    await entree.Writer.WriteAsync(new CommandePerso(4, "Pomme", 4, true));
    entree.Writer.Complete();
});

var filtre = Task.Run(async () =>
{
    await foreach(var message in entree.Reader.ReadAllAsync())
    {
        if (!message.EstTraitee)
        {
            await sortie.Writer.WriteAsync(message);
        }
    }
    sortie.Writer.Complete();
});

var consommateur = Task.Run(async () =>
{
    await foreach(var message in sortie.Reader.ReadAllAsync())
    {
        System.Console.WriteLine(message);
    }
});

await Task.WhenAll(producteur, filtre, consommateur);
#endregion

#region AvecRouter

var entreeR = Channel.CreateUnbounded<CommandePersoRouting>();
var sortieRSMS = Channel.CreateUnbounded<CommandePersoRouting>();
var sortieREmail = Channel.CreateUnbounded<CommandePersoRouting>();

var producteurR = Task.Run(async () =>
{
    await entreeR.Writer.WriteAsync(new CommandePersoRouting(1, "Banane", 5, false, "SMS"));
    await entreeR.Writer.WriteAsync(new CommandePersoRouting(2, "Fraise", 3, false, "SMS"));
    await entreeR.Writer.WriteAsync(new CommandePersoRouting(3, "Kiwi", 8, false, "EMAIL"));
    await entreeR.Writer.WriteAsync(new CommandePersoRouting(4, "Pomme", 4, false, "EMAIL"));
    entreeR.Writer.Complete();
});

var router = Task.Run(async () =>
{
    await foreach(var message in entreeR.Reader.ReadAllAsync())
    {
        if (message.Destination == "SMS")
        {
            await sortieRSMS.Writer.WriteAsync(message);
        }
        else
        {
            await sortieREmail.Writer.WriteAsync(message);
        }
    }
    sortieRSMS.Writer.Complete();
    sortieREmail.Writer.Complete();
});

var consommateurRSMS = Task.Run(async () =>
{
    await foreach(var message in sortieRSMS.Reader.ReadAllAsync())
    {
        System.Console.WriteLine(message);
    }
});

var consommateurREmail = Task.Run(async () =>
{
    await foreach(var message in sortieREmail.Reader.ReadAllAsync())
    {
        System.Console.WriteLine(message);
    }
});

await Task.WhenAll(producteur, router, consommateurRSMS, consommateurREmail);
#endregion

#region AvecRouter

var entreeS = Channel.CreateUnbounded<CommandeValidee>();
var sortieS = Channel.CreateUnbounded<OrdrePreparationCommande>();
var sortieSFruits = Channel.CreateUnbounded<OrdrePreparationCommande>();
var sortieSElectronics = Channel.CreateUnbounded<OrdrePreparationCommande>();

var producteurS = Task.Run(async () =>
{
    await entreeS.Writer.WriteAsync(new CommandeValidee(1, new List<LigneCommande> ()
    {
        new LigneCommande("Pomme", 5_000, "Fruit"),
        new LigneCommande("IPhone 18", 200, "Electronics"),
        new LigneCommande("Banane", 1_000, "Fruit"),
        new LigneCommande("Kiwi", 2_000, "Fruit"),
        new LigneCommande("Ecran PC", 20, "Electronics")
    }));
    await entreeS.Writer.WriteAsync(new CommandeValidee(1, new List<LigneCommande> ()
    {
        new LigneCommande("Samsung Galaxy S24", 100, "Electronics"),
        new LigneCommande("Pêche", 1_000, "Fruit"),
        new LigneCommande("Télévision ecran plat", 500, "Electronics")
    }));
    entreeS.Writer.Complete();
});

var splitter = Task.Run(async () =>
{
    await foreach(var message in entreeS.Reader.ReadAllAsync())
    {
        var ligneCommandesParCategory = message.Lignes.GroupBy(x => x.Category);

        foreach (var category in ligneCommandesParCategory)
        {
            var commandeFinale = new OrdrePreparationCommande(
                message.Numero,
                category.Key,
                category.ToList()
            );

            await sortieS.Writer.WriteAsync(commandeFinale);

            // if (category.Key == "Fruits")
            // {
            //     await sortieSFruits.Writer.WriteAsync(commandeFinale);
            // } else
            // {
            //     await sortieSElectronics.Writer.WriteAsync(commandeFinale);
            // }
        }
    }
    sortieS.Writer.Complete();
    // sortieSFruits.Writer.Complete();
    // sortieSElectronics.Writer.Complete();
});

var consommateurS = Task.Run(async () =>
{
    await foreach(var message in sortieS.Reader.ReadAllAsync())
    {
        System.Console.WriteLine(message);
    }
});

var consommateurSFruits = Task.Run(async () =>
{
    await foreach(var message in sortieSFruits.Reader.ReadAllAsync())
    {
        System.Console.WriteLine(message);
    }
});

var consommateurSElectronics = Task.Run(async () =>
{
    await foreach(var message in sortieSElectronics.Reader.ReadAllAsync())
    {
        System.Console.WriteLine(message);
    }
});

await Task.WhenAll(producteur, splitter, consommateurS);
// await Task.WhenAll(producteur, splitter, consommateurS, consommateurSFruits, consommateurSElectronics);
#endregion

public record CommandePerso(int NumeroCommande, string Article, int Quantity, bool EstTraitee);
public record CommandePersoRouting(int NumeroCommande, string Article, int Quantity, bool EstTraitee, string Destination);

public record LigneCommande(string Produit, int Quantity, string Category);
public record CommandeValidee(int Numero, IReadOnlyList<LigneCommande> Lignes);

public record OrdrePreparationCommande(int NumeroBaseCommande, string Category, IReadOnlyList<LigneCommande> LigneCommandes);