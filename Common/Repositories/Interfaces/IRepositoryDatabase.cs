namespace Common.Repositories.Interfaces
{
    public interface IRepositoryDatabase
    {
        Task<Repository> FindByIdAsync(Guid id);
        Task<List<Repository>> FindAllAsync();
        Task UpsertAsync(Repository repository);
        Task DeleteAsync(Guid id);
        IQueryable<Repository> Query();
        Task<bool> ExistsByIdAsync(Guid id);
    }
}
