namespace Project7.CommunicationHTTP.Models;


public record TodoItemResponse(
    int userId,
    int id,
    string title,
    bool completed
);
