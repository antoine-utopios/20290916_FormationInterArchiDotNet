using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Movie
{
    // [Key]
    public int Id { get; set; } = 0;

    [Column("movie_title", TypeName = "varchar(50)")]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "varchar(50)")]
    public string Director { get; set; } = string.Empty;

    
    [Column(TypeName = "varchar(200)")]
    public string Synopsis { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Id: {Id}, Title: {Title}, Director: {Director}, Synopsis: {Synopsis}";
    }
}