IF TYPE_ID(N'dbo.TT_DCenter_MrnRows') IS NULL
    CREATE TYPE dbo.TT_DCenter_MrnRows AS TABLE
    (
        [Seq] INT NOT NULL PRIMARY KEY,
        [Id] INT NULL,
        [Mrn] NVARCHAR(100) NOT NULL,
        [SpecNo] NVARCHAR(100) NOT NULL,
        [Form] NVARCHAR(200) NULL,
        [FullSpecification] NVARCHAR(400) NULL
    );
