// On crée une instance de contexte de données pour permettre le tracking via ORM entre RAM et BdD
using var context = new ApplicationDbContext();

// On créé en RAM une nouvelle entité
var movieToAdd = new Movie()
{
   Title = "Avatar",
   Director = "James Cameron",
   Synopsis = "Des hommes bleus cherchent à protéger leur maison..." 
};


// Pour ajouter en BdD, il faut l'ajouter au contexte et demander la sauvegarde du contexte (évènement généré puis directement exécuté) 
context.Movies.Add(movieToAdd);
context.SaveChanges();

// Pour lister le contenu du contexte de données (la BdD), on va demander de la récupérer en RAM sous la forme d'une liste
var moviesList = context.Movies.ToList();

// On peut ensuite la traiter comme n'importe quelle variable de type itérable en C#
Console.WriteLine("Listing des films en BdD:");
foreach (var m in moviesList)
{
    Console.WriteLine(m);
    
}

// Pour chercher en base de donnée un élément, on peut ensuite utiliser les méthodes de base de LinQ
var movieAvatar = context.Movies.FirstOrDefault(x => x.Title == "Avatar");

if (movieAvatar is not null) System.Console.WriteLine(movieAvatar);
else System.Console.WriteLine("Pas de film nommé Avatar en BdD");