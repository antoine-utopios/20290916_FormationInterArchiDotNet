namespace Project5.AjoutEFCore.Models;

using System.ComponentModel.DataAnnotations.Schema;

[Table("dogs")]
public class Dog
{
    [Column("dog_id")]
    public int DogId { get; set; }

    [Column(TypeName = "varchar(50)")]
    public string Name { get; set; } = string.Empty;
    public DogBreed Breed { get; set; } = DogBreed.UNKNOWN;
    public int Age { get; set; }

}

public enum DogBreed
{
    UNKNOWN,
    LABRADOR,
    TECKEL,
    GERMAN_SHEPARD,
    SIBERIAN_HUSKY
}