using System.Collections.Concurrent;
using Chat.Storage.Commands;
using Marten;

namespace Chat.EventProcessor.IntegrationTests.Infrastructure;

// Runs a write from another session after the consumer's upsert has loaded the profile and before it
// saves, so the consumer's SaveChangesAsync hits a real Marten concurrency conflict.
public sealed class WriteConflicts
{
    private readonly ConcurrentDictionary<string, Func<IDocumentStore, Task>> _pending = new();

    public void BeforeNextSave(string userId, Func<IDocumentStore, Task> conflictingWrite) =>
        _pending[userId] = conflictingWrite;

    public UpsertProfile Wrap(UpsertProfile upsert, IDocumentStore store) => async (session, parameters, ct) =>
    {
        var result = await upsert(session, parameters, ct);
        if (_pending.TryRemove(parameters.UserId, out var conflictingWrite))
            await conflictingWrite(store);

        return result;
    };
}
