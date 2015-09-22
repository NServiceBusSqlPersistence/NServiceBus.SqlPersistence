﻿namespace NServiceBus.SqlPersistence
{
    public static class TimeoutScriptBuilder
    {

        public static string BuildCreate(string schema, string endpointName)
        {
            return string.Format(@"
IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{0}].[{1}.TimeoutData]') AND type in (N'U'))
BEGIN
    CREATE TABLE [{0}].[{1}.TimeoutData](
	    [Id] [uniqueidentifier] NOT NULL PRIMARY KEY,
	    [Destination] [nvarchar](1024) NULL,
	    [SagaId] [uniqueidentifier] NULL,
	    [State] [varbinary](max) NULL,
	    [Time] [datetime] NULL,
	    [Headers] [xml] NULL
    )
END
", schema, endpointName);
        }
    }
}