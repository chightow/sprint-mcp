using SprintMcp.Application.Abstractions;

namespace SprintMcp.Tests;

internal class MockTransactionManager : ITransactionManager
{
    public async Task<ITransactionScope> BeginAsync(CancellationToken ct = default)
    {
        return new MockTransactionScope();
    }
}

internal class MockTransactionScope : ITransactionScope
{
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
