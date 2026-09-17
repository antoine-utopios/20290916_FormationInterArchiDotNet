namespace Project5.AjoutEFCore.Data;

public interface IRepository<TEntity, TKey> 
    where TEntity : class
    where TKey : struct
{
    public IEnumerable<TEntity> GetAll();

    public TEntity? GetById(TKey id);

    public TEntity Add(TEntity entity);
}