using System.Linq.Expressions;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Tests.TestDoubles;

/// <summary>
/// List-backed repository for handler tests that query with predicates. Changes apply
/// immediately (there is no unit of work to commit); added entities get the next id.
/// </summary>
public sealed class InMemoryRepository<TEntity> : IRepository<TEntity> where TEntity : EntityBase
{
    private long _nextId = 1;

    public List<TEntity> Items { get; } = [];

    public InMemoryRepository(params TEntity[] items)
    {
        foreach (var item in items)
            Add(item);
    }

    public Task<TEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(e => e.Id == id));

    public Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TEntity>>(Items.ToList());

    public Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TEntity>>(Items.Where(predicate.Compile()).ToList());

    public Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        Task.FromResult(Add(entity));

    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Items.Remove(entity);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(e => e.Id == id));

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

    private TEntity Add(TEntity entity)
    {
        if (entity.Id == 0)
            entity.Id = _nextId;
        _nextId = Math.Max(_nextId, entity.Id) + 1;
        Items.Add(entity);
        return entity;
    }
}
