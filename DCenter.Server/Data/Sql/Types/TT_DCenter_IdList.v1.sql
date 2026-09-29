IF TYPE_ID(N'dbo.TT_DCenter_IdList') IS NULL
    CREATE TYPE dbo.TT_DCenter_IdList AS TABLE
    (
        [Id] INT NOT NULL PRIMARY KEY
    );
