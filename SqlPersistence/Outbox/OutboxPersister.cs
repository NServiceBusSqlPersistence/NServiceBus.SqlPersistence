﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Newtonsoft.Json;
using NServiceBus.Extensibility;
using NServiceBus.Outbox;
using IsolationLevel = System.Data.IsolationLevel;

class OutboxPersister : IOutboxStorage
{
    Func<Task<SqlConnection>> connectionBuilder;
    JsonSerializer jsonSerializer;
    Func<TextReader, JsonReader> readerCreator;
    Func<StringBuilder, JsonWriter> writerCreator;
    string storeCommandText;
    string getCommandText;
    string setAsDispatchedCommandText;
    string cleanupCommandText;

    public OutboxPersister(
        Func<Task<SqlConnection>> connectionBuilder, 
        string schema, 
        string endpointName,
        JsonSerializer jsonSerializer,
        Func<TextReader, JsonReader> readerCreator,
        Func<StringBuilder, JsonWriter> writerCreator)
    {
        this.connectionBuilder = connectionBuilder;
        this.jsonSerializer = jsonSerializer;
        this.readerCreator = readerCreator;
        this.writerCreator = writerCreator;
        storeCommandText = $@"
INSERT INTO [{schema}].[{endpointName}OutboxData]
(
    MessageId,
    Operations
)
VALUES
(
    @MessageId,
    @Operations
)";

        cleanupCommandText = $@"
delete from [{schema}].[{endpointName}OutboxData] where Dispatched = true And DispatchedAt < @Date";

        getCommandText = $@"
SELECT
    Dispatched,
    Operations
FROM [{schema}].[{endpointName}OutboxData]
WHERE MessageId = @MessageId";

        setAsDispatchedCommandText = $@"
UPDATE [{schema}].[{endpointName}OutboxData]
SET
    Dispatched = 1,
    DispatchedAt = @DispatchedAt
WHERE MessageId = @MessageId";
    }

    public async Task<OutboxTransaction> BeginTransaction(ContextBag context)
    {
        var sqlConnection = await connectionBuilder();
        var sqlTransaction = sqlConnection.BeginTransaction();
        return new SqlOutboxTransaction(sqlTransaction, sqlConnection);
    }


    public async Task SetAsDispatched(string messageId, ContextBag context)
    {
        using (var connection = await connectionBuilder())
        using (var command = new SqlCommand(setAsDispatchedCommandText, connection))
        {
            command.AddParameter("MessageId", messageId);
            command.AddParameter("DispatchedAt", DateTime.UtcNow);
            await command.ExecuteNonQueryEx();
        }
    }

    public async Task<OutboxMessage> Get(string messageId, ContextBag context)
    {
        using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
        using (var connection = await connectionBuilder())
        using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
        {
            OutboxMessage result;
            using (var command = new SqlCommand(getCommandText, connection, transaction))
            {
                command.AddParameter("MessageId", messageId);
                using (var dataReader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow))
                {
                    if (!await dataReader.ReadAsync())
                    {
                        return null;
                    }
                    var dispatched = dataReader.GetBoolean(0);
                    if (dispatched)
                    {
                        result = new OutboxMessage(messageId, new TransportOperation[0]);
                    }
                    else
                    {
                        using (var textReader = dataReader.GetTextReader(1))
                        using (var jsonReader = readerCreator(textReader))
                        {
                            var transportOperations = jsonSerializer.Deserialize<IEnumerable<SerializableOperation>>(jsonReader)
                                .FromSerializable()
                                .ToArray();
                            result = new OutboxMessage(messageId, transportOperations);
                        }
                    }
                }
            }
            transaction.Commit();
            return result;
        }
    }

    public Task Store(OutboxMessage message, OutboxTransaction transaction, ContextBag context)
    {
        var sqlOutboxTransaction = (SqlOutboxTransaction) transaction;
        var sqlTransaction = sqlOutboxTransaction.SqlTransaction;
        var sqlConnection = sqlOutboxTransaction.SqlConnection;
        return Store(message, sqlTransaction, sqlConnection);
    }

    internal async Task Store(OutboxMessage message, SqlTransaction sqlTransaction, SqlConnection sqlConnection)
    {
        using (var command = new SqlCommand(storeCommandText, sqlConnection, sqlTransaction))
        {
            command.AddParameter("MessageId", message.MessageId);
            command.AddParameter("Operations", OperationsToString(message.TransportOperations));
            await command.ExecuteNonQueryEx();
        }
    }

    string OperationsToString(TransportOperation[] operations)
    {
        var stringBuilder = new StringBuilder();
        using (var jsonWriter = writerCreator(stringBuilder))
        {
            jsonSerializer.Serialize(jsonWriter, operations.ToSerializable());
        }
        return stringBuilder.ToString();
    }

    public async Task RemoveEntriesOlderThan(DateTime dateTime, CancellationToken cancellationToken)
    {
        using (new TransactionScope(TransactionScopeOption.Suppress))
        using (var connection = await connectionBuilder())
        using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
        using (var command = new SqlCommand(cleanupCommandText, connection, transaction))
        {
            command.AddParameter("Date", dateTime);
            await command.ExecuteNonQueryEx(cancellationToken);
        }
    }
}