var publisher = new MessagePublisher("localhost");

System.Console.Write("Connexion au serveur RabbitMQ...");
await publisher.ConnectAsync();
System.Console.WriteLine("Connecté !");

System.Console.Write("Envoi des 5 messages de Toto...");
for (int i = 0; i < 5; i++)
{
    await publisher.SendAsync("Toto", $"Message #{i}");
}
System.Console.WriteLine("Terminé !");