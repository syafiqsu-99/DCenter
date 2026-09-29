IF TYPE_ID(N'dbo.TT_DCenter_BpvcRows') IS NULL
    CREATE TYPE dbo.TT_DCenter_BpvcRows AS TABLE
    (
        [Seq] INT NOT NULL PRIMARY KEY,
        [Id] INT NULL,
        [SpecNo] NVARCHAR(100) NOT NULL,
        [Designation] NVARCHAR(200) NULL,
        [UnsNo] NVARCHAR(100) NULL,
        [PNo] NVARCHAR(50) NOT NULL,
        [MinTensile] NVARCHAR(100) NULL,
        [GroupNo] NVARCHAR(50) NULL,
        [IsoGroup] NVARCHAR(100) NULL,
        [BrazingPNo] NVARCHAR(50) NULL,
        [NominalComposition] NVARCHAR(400) NULL,
        [TypicalProductForm] NVARCHAR(200) NULL,
        [NominalThicknessLimits] NVARCHAR(200) NULL
    );
