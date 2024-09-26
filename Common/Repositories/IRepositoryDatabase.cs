namespace Common.Repositories
{
    public interface IRepositoryDatabase
    {
        Task<Repository> FindByIdAsync(Guid id);
        Task<List<Repository>> FindAllAsync();
        Task UpsertAsync(Repository repository);
        Task DeleteAsync(Guid id);
        IQueryable<Repository> Query();
    }
}
