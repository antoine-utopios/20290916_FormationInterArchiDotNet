using Project7.CommunicationHTTP.Models;

namespace Project7.CommunicationHTTP.Services;

public class TodoItemService
{
    private readonly HttpClient _http;

    public TodoItemService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<TodoItemResponse>> GetAllTodosFromAPI()
    {
        var response = await _http.GetAsync("https://jsonplaceholder.typicode.com/todos");

        if (!response.IsSuccessStatusCode) return new List<TodoItemResponse>();

        return await response.Content.ReadFromJsonAsync<List<TodoItemResponse>>() ?? new List<TodoItemResponse>();
    }
}