using SprintMcp.Application.Abstractions;

namespace SprintMcp.Infrastructure.Persistence;

internal class TransactionManager(AppDbContext db) : ITransactionManager
{
    public async Task<ITransactionScope> BeginAsync(CancellationToken ct = default)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        return new TransactionScope(tx);
    }
}

internal class TransactionScope(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) : ITransactionScope
{
    public Task CommitAsync(CancellationToken ct = default) => tx.CommitAsync(ct);
    public ValueTask DisposeAsync() => tx.DisposeAsync();
}