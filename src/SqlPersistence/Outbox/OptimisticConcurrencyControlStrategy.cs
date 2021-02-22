﻿using System.Data.Common;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Outbox;

class OptimisticConcurrencyControlStrategy : ConcurrencyControlStrategy
{
    SqlDialect sqlDialect;
    OutboxCommands outboxCommands;

    public OptimisticConcurrencyControlStrategy(SqlDialect sqlDialect, OutboxCommands outboxCommands)
    {
        this.sqlDialect = sqlDialect;
        this.outboxCommands = outboxCommands;
    }

    public override Task Begin(string messageId, DbConnection connection, DbTransaction transaction)
    {
        return Task.FromResult(0);
    }

    public override async Task Complete(OutboxMessage outboxMessage, DbConnection connection, DbTransaction transaction, ContextBag context)
    {
        string json = Serializer.Serialize(outboxMessage.TransportOperations.ToSerializable());
        json = sqlDialect.AddOutboxPadding(json);

        using (var command = sqlDialect.CreateCommand(connection))
        {
            command.CommandText = outboxCommands.OptimisticStore;
            command.Transaction = transaction;
            command.AddParameter("MessageId", outboxMessage.MessageId);
            command.AddJsonParameter("Operations", json);
            command.AddParameter("PersistenceVersion", StaticVersions.PersistenceVersion);
            await command.ExecuteNonQueryEx().ConfigureAwait(false);
        }
    }
}