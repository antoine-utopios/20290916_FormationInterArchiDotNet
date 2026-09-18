var receiver = new MessageReceiver("localhost");

System.Console.Write("Connexion au serveur RabbitMQ...");
await receiver.ConnectAsync();
System.Console.WriteLine("Connecté !");

System.Console.WriteLine("Lecture des messages...");
await receiver.StartListeningAsync(message =>
{
   System.Console.WriteLine($"[{message.author}]: {message.Text}"); 
});
await Task.Delay(Timeout.Infinite);

System.Console.WriteLine("Lecture des messages terminée !");