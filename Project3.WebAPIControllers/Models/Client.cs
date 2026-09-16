namespace Project3.WebAPIControllers.Models;

public class Client
{
    public int Id { get; set; } = 0;

    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}