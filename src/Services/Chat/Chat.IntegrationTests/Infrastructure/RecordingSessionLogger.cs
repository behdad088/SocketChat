using Marten;
using Marten.Services;
using Npgsql;

namespace Chat.IntegrationTests.Infrastructure;

public sealed class RecordingSessionLogger : IMartenSessionLogger
{
    public List<NpgsqlCommand> Commands { get; } = [];

    public void OnBeforeExecute(NpgsqlCommand command) => Commands.Add(command);

    public void OnBeforeExecute(NpgsqlBatch batch)
    {
        foreach (var batchCommand in batch.BatchCommands)
        {
            var command = new NpgsqlCommand(batchCommand.CommandText);
            foreach (NpgsqlParameter parameter in batchCommand.Parameters)
                command.Parameters.Add(parameter.Clone());
            Commands.Add(command);
        }
    }

    public void LogSuccess(NpgsqlCommand command)
    {
    }

    public void LogSuccess(NpgsqlBatch batch)
    {
    }

    public void LogFailure(NpgsqlCommand command, Exception ex)
    {
    }

    public void LogFailure(NpgsqlBatch batch, Exception ex)
    {
    }

    public void LogFailure(Exception ex, string message)
    {
    }

    public void RecordSavedChanges(IDocumentSession session, IChangeSet commit)
    {
    }
}
