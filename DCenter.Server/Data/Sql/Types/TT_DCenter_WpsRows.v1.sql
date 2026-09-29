IF TYPE_ID(N'dbo.TT_DCenter_WpsRows') IS NULL
    CREATE TYPE dbo.TT_DCenter_WpsRows AS TABLE
    (
        [Seq] INT NOT NULL PRIMARY KEY,
        [Id] INT NULL,
        [WpsNo] NVARCHAR(200) NOT NULL,
        [PNo] NVARCHAR(50) NOT NULL,
        [BaseMetal] NVARCHAR(200) NULL,
        [Process] NVARCHAR(100) NULL
    );
