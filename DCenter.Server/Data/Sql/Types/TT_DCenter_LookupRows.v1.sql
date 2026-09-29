IF TYPE_ID(N'dbo.TT_DCenter_LookupRows') IS NULL
    CREATE TYPE dbo.TT_DCenter_LookupRows AS TABLE
    (
        [Seq] INT NOT NULL PRIMARY KEY,
        [Id] INT NULL,
        [Category] NVARCHAR(50) NOT NULL,
        [Value] NVARCHAR(200) NOT NULL,
        [SortOrder] INT NOT NULL,
        [IsActive] BIT NOT NULL
    );
