using Microsoft.EntityFrameworkCore;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.Invariants;
using SprintMcp.Application.Services;
using SprintMcp.Domain.ValueObjects;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Infrastructure.Persistence.Repositories;

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

internal static class EventTestHelpers
{
    public static (EventStore eventStore, InvariantEngine invariantEngine) CreateEventDeps(AppDbContext ctx)
    {
        var eventStore = new EventStore(ctx);
        var invariantContext = new InvariantContext(ctx);
        var invariants = new SprintMcp.Application.Abstractions.IInvariant[]
        {
            new PhaseGateInvariant(),
            new TicketStatusTransitionInvariant()
        };
        var engine = new InvariantEngine(invariants, invariantContext);
        return (eventStore, engine);
    }
}
