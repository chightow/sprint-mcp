namespace SprintMcp.Application.Abstractions;

public interface ITransactionManager
{
    Task<ITransactionScope> BeginAsync(CancellationToken ct = default);
}

public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}