/*
    DCenter stored procedures and table types. Run this against the DCenter database (the
    DefaultConnection database) after the EF Core migrations and after DCenter_SourceViews.sql.
    Safe to re-run: re-run the whole file after every release that changes it.

    Naming
    - dbo.SP_DCenter_<Module>_<Action>   stored procedures; they read and write the DCenter_* tables
                                          directly (views are only used over OracleBetsyDB)
    - dbo.TT_DCenter_<Name>              table types for multi-row parameters

    A procedure that must update or delete a row that no longer exists raises THROW 50001; the API
    turns that into the same "changed by someone else" answer as before.

    A table type cannot be altered in place. To change one: add DROP PROCEDURE IF EXISTS for every
    procedure that uses it and DROP TYPE IF EXISTS for the type just above its CREATE TYPE below,
    re-run this file, then remove those DROP lines again.

    With sqlcmd, pass -I (QUOTED_IDENTIFIER on):
        sqlcmd -S <server> -d DCenter -I -b -i DCenter_StoredProcedures.sql
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ===== Table types ===== */

IF TYPE_ID(N'dbo.TT_DCenter_IdList') IS NULL
    CREATE TYPE dbo.TT_DCenter_IdList AS TABLE
    (
        [Id] INT NOT NULL PRIMARY KEY
    );
GO

IF TYPE_ID(N'dbo.TT_DCenter_IdOrder') IS NULL
    CREATE TYPE dbo.TT_DCenter_IdOrder AS TABLE
    (
        [Id] INT NOT NULL PRIMARY KEY,
        [SortOrder] INT NOT NULL
    );
GO

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
GO

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
GO

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
GO

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
GO

/* ===== Settings: welders ===== */

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WelderName], [WelderNo], [IsActive], [UsageScope]
    FROM dbo.DCenter_Welders
    ORDER BY [WelderName];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Search
    @Q NVARCHAR(4000),
    @StockOnly BIT,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Take) [Id], [WelderName], [WelderNo], [IsActive], [UsageScope]
    FROM dbo.DCenter_Welders
    WHERE [IsActive] = 1
      AND (@StockOnly = 0 OR [UsageScope] = N'ReportAndStock')
      AND (@Q = N'' OR CHARINDEX(@Q, [WelderName]) > 0 OR CHARINDEX(@Q, [WelderNo]) > 0)
    ORDER BY [WelderName];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Get
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WelderName], [WelderNo], [IsActive], [UsageScope]
    FROM dbo.DCenter_Welders
    WHERE [Id] = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_NumberHolder
    @WelderNo NVARCHAR(50),
    @ExcludeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) [WelderName] AS [Value]
    FROM dbo.DCenter_Welders
    WHERE [WelderNo] = @WelderNo AND [Id] <> @ExcludeId;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_InUse
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE
        WHEN EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableMovements WHERE [WelderId] = @Id)
          OR EXISTS (SELECT 1 FROM dbo.DCenter_HoldingRecords WHERE [WelderId] = @Id)
        THEN 1 ELSE 0 END AS BIT) AS [Value];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Insert
    @WelderName NVARCHAR(200),
    @WelderNo NVARCHAR(50),
    @IsActive BIT,
    @UsageScope NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    INSERT INTO dbo.DCenter_Welders ([WelderName], [WelderNo], [IsActive], [UsageScope])
    OUTPUT inserted.[Id] INTO @Ids
    VALUES (@WelderName, @WelderNo, @IsActive, @UsageScope);

    SELECT v.[Id], v.[WelderName], v.[WelderNo], v.[IsActive], v.[UsageScope]
    FROM dbo.DCenter_Welders v
    JOIN @Ids i ON i.[Id] = v.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Update
    @Id INT,
    @WelderName NVARCHAR(200),
    @WelderNo NVARCHAR(50),
    @IsActive BIT,
    @UsageScope NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.DCenter_Welders
    SET [WelderName] = @WelderName, [WelderNo] = @WelderNo, [IsActive] = @IsActive, [UsageScope] = @UsageScope
    WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The welder was changed or deleted by someone else.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_SetScope
    @Ids dbo.TT_DCenter_IdList READONLY,
    @UsageScope NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE w
    SET [UsageScope] = @UsageScope
    FROM dbo.DCenter_Welders w
    JOIN @Ids i ON i.[Id] = w.[Id];

    SELECT @@ROWCOUNT AS [Value];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_Welders WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The welder was changed or deleted by someone else.', 1;
END
GO

/* ===== Settings: dropdown lists ===== */

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_List
    @Category NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Category], [Value], [SortOrder], [IsActive]
    FROM dbo.DCenter_Lookups
    WHERE @Category IS NULL OR [Category] = @Category
    ORDER BY [Category], [SortOrder], [Value];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_Exists
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.DCenter_Lookups WHERE [Id] = @Id) THEN 1 ELSE 0 END AS BIT) AS [Value];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_IsDuplicate
    @Category NVARCHAR(50),
    @Value NVARCHAR(200),
    @ExcludeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM dbo.DCenter_Lookups
        WHERE [Category] = @Category AND [Value] = @Value AND [Id] <> @ExcludeId
    ) THEN 1 ELSE 0 END AS BIT) AS [Value];
END
GO

-- Rows with an Id update that lookup; rows without one are inserted in Seq order. Returns the inserted rows.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_Save
    @Rows dbo.TT_DCenter_LookupRows READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    BEGIN TRANSACTION;

    UPDATE l
    SET [Category] = r.[Category], [Value] = r.[Value], [SortOrder] = r.[SortOrder], [IsActive] = r.[IsActive]
    FROM dbo.DCenter_Lookups l
    JOIN @Rows r ON r.[Id] = l.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rows WHERE [Id] IS NOT NULL)
        THROW 50001, N'A dropdown value was changed or deleted by someone else.', 1;

    INSERT INTO dbo.DCenter_Lookups ([Category], [Value], [SortOrder], [IsActive])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [Category], [Value], [SortOrder], [IsActive]
    FROM @Rows
    WHERE [Id] IS NULL
    ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT v.[Id], v.[Category], v.[Value], v.[SortOrder], v.[IsActive]
    FROM dbo.DCenter_Lookups v
    JOIN @Ids i ON i.[Id] = v.[Id]
    ORDER BY v.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_Reorder
    @Rows dbo.TT_DCenter_IdOrder READONLY
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE l
    SET [SortOrder] = r.[SortOrder]
    FROM dbo.DCenter_Lookups l
    JOIN @Rows r ON r.[Id] = l.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_Lookups WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The dropdown value was changed or deleted by someone else.', 1;
END
GO

/* ===== Settings: Process–Type links ===== */

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_ProcessTypeLink_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Process], [Type]
    FROM dbo.DCenter_ProcessTypeLinks
    ORDER BY [Process], [Type];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_ProcessTypeLink_Exists
    @Process NVARCHAR(200),
    @Type NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM dbo.DCenter_ProcessTypeLinks WHERE [Process] = @Process AND [Type] = @Type
    ) THEN 1 ELSE 0 END AS BIT) AS [Value];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_ProcessTypeLink_Insert
    @Process NVARCHAR(200),
    @Type NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    INSERT INTO dbo.DCenter_ProcessTypeLinks ([Process], [Type])
    OUTPUT inserted.[Id] INTO @Ids
    VALUES (@Process, @Type);

    SELECT v.[Id], v.[Process], v.[Type]
    FROM dbo.DCenter_ProcessTypeLinks v
    JOIN @Ids i ON i.[Id] = v.[Id];
END
GO

-- Returns the number of rows deleted (0 when the link no longer exists).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_ProcessTypeLink_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_ProcessTypeLinks WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [Value];
END
GO

/* ===== Settings: WPS ===== */

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Wps_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WpsNo], [PNo], [BaseMetal], [Process]
    FROM dbo.DCenter_WpsItems
    ORDER BY [WpsNo], [PNo];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Wps_Get
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WpsNo], [PNo], [BaseMetal], [Process]
    FROM dbo.DCenter_WpsItems
    WHERE [Id] = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Wps_FindByKey
    @WpsNo NVARCHAR(200),
    @PNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WpsNo], [PNo], [BaseMetal], [Process]
    FROM dbo.DCenter_WpsItems
    WHERE [WpsNo] = @WpsNo AND [PNo] = @PNo;
END
GO

-- Rows with an Id update that row; rows without one are inserted in Seq order. Returns the inserted rows.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Wps_Save
    @Rows dbo.TT_DCenter_WpsRows READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    BEGIN TRANSACTION;

    UPDATE t
    SET [WpsNo] = r.[WpsNo], [PNo] = r.[PNo], [BaseMetal] = r.[BaseMetal], [Process] = r.[Process]
    FROM dbo.DCenter_WpsItems t
    JOIN @Rows r ON r.[Id] = t.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rows WHERE [Id] IS NOT NULL)
        THROW 50001, N'A WPS row was changed or deleted by someone else.', 1;

    INSERT INTO dbo.DCenter_WpsItems ([WpsNo], [PNo], [BaseMetal], [Process])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [WpsNo], [PNo], [BaseMetal], [Process]
    FROM @Rows
    WHERE [Id] IS NULL
    ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT v.[Id], v.[WpsNo], v.[PNo], v.[BaseMetal], v.[Process]
    FROM dbo.DCenter_WpsItems v
    JOIN @Ids i ON i.[Id] = v.[Id]
    ORDER BY v.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Wps_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_WpsItems WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The WPS row was changed or deleted by someone else.', 1;
END
GO

/* ===== Settings: MRN ===== */

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Mrn], [SpecNo], [Form], [FullSpecification]
    FROM dbo.DCenter_MrnSpecs
    ORDER BY [Mrn], [SpecNo];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_Get
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Mrn], [SpecNo], [Form], [FullSpecification]
    FROM dbo.DCenter_MrnSpecs
    WHERE [Id] = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_FindByKey
    @Mrn NVARCHAR(100),
    @SpecNo NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Mrn], [SpecNo], [Form], [FullSpecification]
    FROM dbo.DCenter_MrnSpecs
    WHERE [Mrn] = @Mrn AND [SpecNo] = @SpecNo;
END
GO

-- Rows with an Id update that row; rows without one are inserted in Seq order. Returns the inserted rows.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_Save
    @Rows dbo.TT_DCenter_MrnRows READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    BEGIN TRANSACTION;

    UPDATE t
    SET [Mrn] = r.[Mrn], [SpecNo] = r.[SpecNo], [Form] = r.[Form], [FullSpecification] = r.[FullSpecification]
    FROM dbo.DCenter_MrnSpecs t
    JOIN @Rows r ON r.[Id] = t.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rows WHERE [Id] IS NOT NULL)
        THROW 50001, N'A MRN row was changed or deleted by someone else.', 1;

    INSERT INTO dbo.DCenter_MrnSpecs ([Mrn], [SpecNo], [Form], [FullSpecification])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [Mrn], [SpecNo], [Form], [FullSpecification]
    FROM @Rows
    WHERE [Id] IS NULL
    ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT v.[Id], v.[Mrn], v.[SpecNo], v.[Form], v.[FullSpecification]
    FROM dbo.DCenter_MrnSpecs v
    JOIN @Ids i ON i.[Id] = v.[Id]
    ORDER BY v.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_MrnSpecs WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The MRN row was changed or deleted by someone else.', 1;
END
GO

/* ===== Settings: BPVC IX ===== */

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM dbo.DCenter_BpvcIx
    ORDER BY [SpecNo], [Designation], [UnsNo], [PNo];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_Get
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM dbo.DCenter_BpvcIx
    WHERE [Id] = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_FindByKey
    @SpecNo NVARCHAR(100),
    @PNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM dbo.DCenter_BpvcIx
    WHERE [SpecNo] = @SpecNo AND [PNo] = @PNo;
END
GO

-- Rows with an Id update that row; rows without one are inserted in Seq order. Returns the inserted rows.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_Save
    @Rows dbo.TT_DCenter_BpvcRows READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    BEGIN TRANSACTION;

    UPDATE t
    SET [SpecNo] = r.[SpecNo], [Designation] = r.[Designation], [UnsNo] = r.[UnsNo], [PNo] = r.[PNo], [MinTensile] = r.[MinTensile], [GroupNo] = r.[GroupNo], [IsoGroup] = r.[IsoGroup], [BrazingPNo] = r.[BrazingPNo], [NominalComposition] = r.[NominalComposition], [TypicalProductForm] = r.[TypicalProductForm], [NominalThicknessLimits] = r.[NominalThicknessLimits]
    FROM dbo.DCenter_BpvcIx t
    JOIN @Rows r ON r.[Id] = t.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rows WHERE [Id] IS NOT NULL)
        THROW 50001, N'A BPVC row was changed or deleted by someone else.', 1;

    INSERT INTO dbo.DCenter_BpvcIx ([SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM @Rows
    WHERE [Id] IS NULL
    ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT v.[Id], v.[SpecNo], v.[Designation], v.[UnsNo], v.[PNo], v.[MinTensile], v.[GroupNo], v.[IsoGroup], v.[BrazingPNo], v.[NominalComposition], v.[TypicalProductForm], v.[NominalThicknessLimits]
    FROM dbo.DCenter_BpvcIx v
    JOIN @Ids i ON i.[Id] = v.[Id]
    ORDER BY v.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_BpvcIx WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The BPVC row was changed or deleted by someone else.', 1;
END
GO
