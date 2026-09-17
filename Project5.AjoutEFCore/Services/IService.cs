namespace Project5.AjoutEFCore.Services;

public interface IService<TDtoResponse, TDtoRequest, TKey> 
    where TDtoResponse : class 
    where TDtoRequest : class 
    where TKey : struct
{
    public IEnumerable<TDtoResponse> GetAll();
    public TDtoResponse? GetById(TKey entityId);

    public TDtoResponse? Add(TDtoRequest request);
} 