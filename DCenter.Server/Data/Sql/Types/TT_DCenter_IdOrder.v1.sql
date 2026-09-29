IF TYPE_ID(N'dbo.TT_DCenter_IdOrder') IS NULL
    CREATE TYPE dbo.TT_DCenter_IdOrder AS TABLE
    (
        [Id] INT NOT NULL PRIMARY KEY,
        [SortOrder] INT NOT NULL
    );
