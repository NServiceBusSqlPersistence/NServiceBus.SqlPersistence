﻿using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Persistence;

class SynchronizedStorage : ISynchronizedStorage
{
    Func<Task<SqlConnection>> connectionBuilder;

    public SynchronizedStorage(Func<Task<SqlConnection>> connectionBuilder)
    {
        this.connectionBuilder = connectionBuilder;
    }

    public async Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag)
    {
        var connection = await connectionBuilder();
        var transaction = connection.BeginTransaction();
        return new StorageSession(connection, transaction,true);
    }
}