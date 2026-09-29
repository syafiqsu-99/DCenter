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

IF TYPE_ID(N'dbo.TT_DCenter_ReportJoints') IS NULL
    CREATE TYPE dbo.TT_DCenter_ReportJoints AS TABLE
    (
        [Seq] INT NOT NULL PRIMARY KEY,
        [JointNumber] INT NOT NULL,
        [PartDescLeft] NVARCHAR(MAX) NULL,
        [PartNoLeft] NVARCHAR(MAX) NULL,
        [HeatNumberLeft] NVARCHAR(MAX) NULL,
        [PartDescRight] NVARCHAR(MAX) NULL,
        [PartNoRight] NVARCHAR(MAX) NULL,
        [HeatNumberRight] NVARCHAR(MAX) NULL,
        [WpsNo] NVARCHAR(MAX) NULL,
        [Rev] NVARCHAR(MAX) NULL,
        [WelderName] NVARCHAR(MAX) NULL,
        [WelderNo] NVARCHAR(MAX) NULL
    );
GO

IF TYPE_ID(N'dbo.TT_DCenter_ReportJointMaterials') IS NULL
    CREATE TYPE dbo.TT_DCenter_ReportJointMaterials AS TABLE
    (
        [JointSeq] INT NOT NULL,
        [Seq] INT NOT NULL,
        [ColumnNumber] INT NOT NULL,
        [Process] NVARCHAR(MAX) NULL,
        [Size] NVARCHAR(MAX) NULL,
        [Type] NVARCHAR(MAX) NULL,
        [Manuf] NVARCHAR(MAX) NULL,
        [HeatLot] NVARCHAR(MAX) NULL,
        PRIMARY KEY ([JointSeq], [Seq])
    );
GO

IF TYPE_ID(N'dbo.TT_DCenter_TextList') IS NULL
    CREATE TYPE dbo.TT_DCenter_TextList AS TABLE
    (
        [Seq] INT NOT NULL PRIMARY KEY,
        [Value] NVARCHAR(4000) NOT NULL
    );
GO

IF TYPE_ID(N'dbo.TT_DCenter_IdStatus') IS NULL
    CREATE TYPE dbo.TT_DCenter_IdStatus AS TABLE
    (
        [Id] INT NOT NULL PRIMARY KEY,
        [Status] NVARCHAR(20) NOT NULL
    );
GO

IF TYPE_ID(N'dbo.TT_DCenter_MovementRows') IS NULL
    CREATE TYPE dbo.TT_DCenter_MovementRows AS TABLE
    (
        [Seq] INT NOT NULL PRIMARY KEY,
        [TxnNo] NVARCHAR(20) NOT NULL,
        [TxnType] NVARCHAR(20) NOT NULL,
        [TxnDate] DATE NOT NULL,
        [LotId] INT NOT NULL,
        [QuantityKg] DECIMAL(10, 2) NOT NULL,
        [FromStage] NVARCHAR(20) NULL,
        [ToStage] NVARCHAR(20) NULL,
        [FromCompartmentId] INT NULL,
        [ToCompartmentId] INT NULL,
        [BakingRecordId] INT NULL,
        [Source] NVARCHAR(20) NULL,
        [Requestor] NVARCHAR(200) NULL,
        [WelderId] INT NULL,
        [Reason] NVARCHAR(40) NULL,
        [CountedQtyKg] DECIMAL(10, 2) NULL,
        [ReferenceNo] NVARCHAR(60) NULL,
        [Remarks] NVARCHAR(500) NULL,
        [IsVoided] BIT NOT NULL,
        [VoidsMovementId] INT NULL,
        [CreatedBy] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL
    );
GO

IF TYPE_ID(N'dbo.TT_DCenter_BakingRows') IS NULL
    CREATE TYPE dbo.TT_DCenter_BakingRows AS TABLE
    (
        [Seq] INT NOT NULL PRIMARY KEY,
        [BakingNo] NVARCHAR(20) NOT NULL,
        [LotId] INT NOT NULL,
        [QuantityKg] DECIMAL(10, 2) NOT NULL,
        [PersonInCharge] NVARCHAR(100) NOT NULL,
        [BakingDate] DATE NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [Remarks] NVARCHAR(500) NULL,
        [CreatedBy] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL
    );
GO

IF TYPE_ID(N'dbo.TT_DCenter_BakingTimes') IS NULL
    CREATE TYPE dbo.TT_DCenter_BakingTimes AS TABLE
    (
        [Id] INT NOT NULL PRIMARY KEY,
        [BakeStart] DATETIME2 NULL,
        [BakeStop] DATETIME2 NULL,
        [RebakeStart] DATETIME2 NULL,
        [RebakeStop] DATETIME2 NULL
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

/* ===== Weld reports ===== */

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.[Id], r.[WorkOrderNumber], r.[PartNo], r.[Description],
           (SELECT COUNT(*) FROM dbo.DCenter_Joints j WHERE j.[ReportId] = r.[Id]) AS [JointCount],
           CASE WHEN r.[CompletedAt] IS NULL THEN N'Draft' ELSE N'Completed' END AS [Status],
           r.[UpdatedAt]
    FROM dbo.DCenter_Reports r
    ORDER BY r.[UpdatedAt] DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_Get
    @WorkOrderNumber NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WorkOrderNumber], [ReportRequired], [DateWelded], [PartNo], [Description], [MaterialSpec1], [MaterialSpec2], [MaterialSpec3], [Grade1], [Grade2], [Grade3], [PNumber1], [PNumber2], [PNumber3], [EngineerSupervisor], [QaInspector], [CreatedAt], [UpdatedAt], [CompletedAt], [RowVersion]
    FROM dbo.DCenter_Reports
    WHERE [WorkOrderNumber] = @WorkOrderNumber;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_Joints
    @ReportId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [ReportId], [JointNumber], [PartDescLeft], [PartNoLeft], [HeatNumberLeft], [PartDescRight], [PartNoRight], [HeatNumberRight], [WpsNo], [Rev], [WelderName], [WelderNo]
    FROM dbo.DCenter_Joints
    WHERE [ReportId] = @ReportId
    ORDER BY [Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_JointMaterials
    @ReportId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.[Id], m.[JointId], m.[ColumnNumber], m.[Process], m.[Size], m.[Type], m.[Manuf], m.[HeatLot]
    FROM dbo.DCenter_JointMaterials m
    JOIN dbo.DCenter_Joints j ON j.[Id] = m.[JointId]
    WHERE j.[ReportId] = @ReportId
    ORDER BY m.[Id];
END
GO

-- @Id NULL inserts a new report; otherwise the report is updated only while its RowVersion still
-- matches (THROW 50001 when it does not) and its joints are replaced. Returns the report Id.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_Save
    @Id INT,
    @RowVersion BINARY(8),
    @WorkOrderNumber NVARCHAR(100),
    @ReportRequired BIT,
    @DateWelded DATE,
    @PartNo NVARCHAR(MAX),
    @Description NVARCHAR(MAX),
    @MaterialSpec1 NVARCHAR(MAX),
    @MaterialSpec2 NVARCHAR(MAX),
    @MaterialSpec3 NVARCHAR(MAX),
    @Grade1 NVARCHAR(MAX),
    @Grade2 NVARCHAR(MAX),
    @Grade3 NVARCHAR(MAX),
    @PNumber1 NVARCHAR(MAX),
    @PNumber2 NVARCHAR(MAX),
    @PNumber3 NVARCHAR(MAX),
    @EngineerSupervisor NVARCHAR(MAX),
    @QaInspector NVARCHAR(MAX),
    @CreatedAt DATETIME2,
    @UpdatedAt DATETIME2,
    @Action NVARCHAR(50),
    @Details NVARCHAR(1000),
    @OccurredAt DATETIME2,
    @Joints dbo.TT_DCenter_ReportJoints READONLY,
    @Materials dbo.TT_DCenter_ReportJointMaterials READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @JointIds TABLE ([Id] INT NOT NULL PRIMARY KEY);

    BEGIN TRANSACTION;

    IF @Id IS NULL
    BEGIN
        INSERT INTO dbo.DCenter_Reports ([WorkOrderNumber], [ReportRequired], [DateWelded], [PartNo], [Description], [MaterialSpec1], [MaterialSpec2], [MaterialSpec3], [Grade1], [Grade2], [Grade3], [PNumber1], [PNumber2], [PNumber3], [EngineerSupervisor], [QaInspector], [CreatedAt], [UpdatedAt])
        VALUES (@WorkOrderNumber, @ReportRequired, @DateWelded, @PartNo, @Description, @MaterialSpec1, @MaterialSpec2, @MaterialSpec3, @Grade1, @Grade2, @Grade3, @PNumber1, @PNumber2, @PNumber3, @EngineerSupervisor, @QaInspector, @CreatedAt, @UpdatedAt);
        SET @Id = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.DCenter_Reports
        SET [ReportRequired] = @ReportRequired,
        [DateWelded] = @DateWelded,
        [PartNo] = @PartNo,
        [Description] = @Description,
        [MaterialSpec1] = @MaterialSpec1,
        [MaterialSpec2] = @MaterialSpec2,
        [MaterialSpec3] = @MaterialSpec3,
        [Grade1] = @Grade1,
        [Grade2] = @Grade2,
        [Grade3] = @Grade3,
        [PNumber1] = @PNumber1,
        [PNumber2] = @PNumber2,
        [PNumber3] = @PNumber3,
        [EngineerSupervisor] = @EngineerSupervisor,
        [QaInspector] = @QaInspector,
        [UpdatedAt] = @UpdatedAt
        WHERE [Id] = @Id AND [RowVersion] = @RowVersion;

        IF @@ROWCOUNT = 0
            THROW 50001, N'The report was changed by someone else.', 1;

        DELETE m
        FROM dbo.DCenter_JointMaterials m
        JOIN dbo.DCenter_Joints j ON j.[Id] = m.[JointId]
        WHERE j.[ReportId] = @Id;

        DELETE FROM dbo.DCenter_Joints WHERE [ReportId] = @Id;
    END

    INSERT INTO dbo.DCenter_Joints ([ReportId], [JointNumber], [PartDescLeft], [PartNoLeft], [HeatNumberLeft], [PartDescRight], [PartNoRight], [HeatNumberRight], [WpsNo], [Rev], [WelderName], [WelderNo])
    OUTPUT inserted.[Id] INTO @JointIds
    SELECT @Id, [JointNumber], [PartDescLeft], [PartNoLeft], [HeatNumberLeft], [PartDescRight], [PartNoRight], [HeatNumberRight], [WpsNo], [Rev], [WelderName], [WelderNo]
    FROM @Joints
    ORDER BY [Seq];

    -- Identity values follow the ORDER BY above, so the n-th new Id belongs to the n-th joint.
    WITH NewIds AS (SELECT [Id], ROW_NUMBER() OVER (ORDER BY [Id]) AS [N] FROM @JointIds),
         Seqs AS (SELECT [Seq], ROW_NUMBER() OVER (ORDER BY [Seq]) AS [N] FROM @Joints)
    INSERT INTO dbo.DCenter_JointMaterials ([JointId], [ColumnNumber], [Process], [Size], [Type], [Manuf], [HeatLot])
    SELECT n.[Id], m.[ColumnNumber], m.[Process], m.[Size], m.[Type], m.[Manuf], m.[HeatLot]
    FROM @Materials m
    JOIN Seqs s ON s.[Seq] = m.[JointSeq]
    JOIN NewIds n ON n.[N] = s.[N]
    ORDER BY m.[JointSeq], m.[Seq];

    INSERT INTO dbo.DCenter_ReportStatusEvents ([ReportId], [Action], [OccurredAt], [Details])
    VALUES (@Id, @Action, @OccurredAt, @Details);

    COMMIT TRANSACTION;

    SELECT @Id AS [Value];
END
GO

-- Completes or reopens a report while its RowVersion still matches (THROW 50001 when it does not).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_SetStatus
    @Id INT,
    @RowVersion BINARY(8),
    @CompletedAt DATETIME2,
    @UpdatedAt DATETIME2,
    @Action NVARCHAR(50),
    @Details NVARCHAR(1000),
    @OccurredAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    UPDATE dbo.DCenter_Reports
    SET [CompletedAt] = @CompletedAt, [UpdatedAt] = @UpdatedAt
    WHERE [Id] = @Id AND [RowVersion] = @RowVersion;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The report was changed by someone else.', 1;

    INSERT INTO dbo.DCenter_ReportStatusEvents ([ReportId], [Action], [OccurredAt], [Details])
    VALUES (@Id, @Action, @OccurredAt, @Details);

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_History
    @ReportId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Action], [OccurredAt], [Details]
    FROM dbo.DCenter_ReportStatusEvents
    WHERE [ReportId] = @ReportId
    ORDER BY [OccurredAt] DESC;
END
GO

-- Joints, materials and status events go with the report (ON DELETE CASCADE).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_Reports WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The report was changed or deleted by someone else.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_DashboardReports
    @WindowStart DATE,
    @WindowStartAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.[WorkOrderNumber], r.[PartNo], r.[Description], r.[CompletedAt], r.[UpdatedAt], r.[DateWelded],
           (SELECT COUNT(*) FROM dbo.DCenter_Joints j WHERE j.[ReportId] = r.[Id]) AS [JointCount]
    FROM dbo.DCenter_Reports r
    WHERE r.[ReportRequired] = 1
      AND (r.[CompletedAt] IS NULL OR r.[CompletedAt] >= @WindowStartAt OR r.[DateWelded] >= @WindowStart);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_DashboardWelders
    @WindowStart DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT j.[WelderNo], j.[WelderName], COUNT(*) AS [Count]
    FROM dbo.DCenter_Joints j
    JOIN dbo.DCenter_Reports r ON r.[Id] = j.[ReportId]
    WHERE r.[ReportRequired] = 1 AND r.[DateWelded] >= @WindowStart
      AND j.[WelderNo] IS NOT NULL AND j.[WelderNo] <> N''
    GROUP BY j.[WelderNo], j.[WelderName];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_DashboardWps
    @WindowStart DATE,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Take) j.[WpsNo], COUNT(*) AS [Count]
    FROM dbo.DCenter_Joints j
    JOIN dbo.DCenter_Reports r ON r.[Id] = j.[ReportId]
    WHERE r.[ReportRequired] = 1 AND r.[DateWelded] >= @WindowStart
      AND j.[WpsNo] IS NOT NULL AND j.[WpsNo] <> N''
    GROUP BY j.[WpsNo]
    ORDER BY COUNT(*) DESC;
END
GO

-- @Field is welder, wps, heat or heatLot. HeatLots lists the joint's electrode heat/lots in column
-- order, separated by CHAR(31).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Report_Trace
    @Field NVARCHAR(20),
    @Q NVARCHAR(4000),
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Take)
           r.[WorkOrderNumber], r.[PartNo], r.[CompletedAt], r.[DateWelded],
           j.[JointNumber], j.[WpsNo], j.[WelderName], j.[WelderNo], j.[HeatNumberLeft], j.[HeatNumberRight],
           (SELECT STRING_AGG(m.[HeatLot], NCHAR(31)) WITHIN GROUP (ORDER BY m.[ColumnNumber])
            FROM dbo.DCenter_JointMaterials m WHERE m.[JointId] = j.[Id]) AS [HeatLots]
    FROM dbo.DCenter_Joints j
    JOIN dbo.DCenter_Reports r ON r.[Id] = j.[ReportId]
    WHERE (@Field = N'welder' AND (CHARINDEX(@Q, j.[WelderNo]) > 0 OR CHARINDEX(@Q, j.[WelderName]) > 0))
       OR (@Field = N'wps' AND CHARINDEX(@Q, j.[WpsNo]) > 0)
       OR (@Field = N'heat' AND (CHARINDEX(@Q, j.[HeatNumberLeft]) > 0 OR CHARINDEX(@Q, j.[HeatNumberRight]) > 0))
       OR (@Field = N'heatLot' AND EXISTS (
            SELECT 1 FROM dbo.DCenter_JointMaterials m WHERE m.[JointId] = j.[Id] AND CHARINDEX(@Q, m.[HeatLot]) > 0))
    ORDER BY r.[DateWelded] DESC, r.[WorkOrderNumber], j.[JointNumber];
END
GO

/* ===== Consumables ===== */

-- Next number from one of the fixed DCenter sequences.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Sequence_Next
    @Sequence NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    IF @Sequence = N'DCenter_ConsumableTxnSeq' SELECT NEXT VALUE FOR dbo.DCenter_ConsumableTxnSeq AS [Value];
    ELSE IF @Sequence = N'DCenter_BakingNoSeq' SELECT NEXT VALUE FOR dbo.DCenter_BakingNoSeq AS [Value];
    ELSE IF @Sequence = N'DCenter_HoldingNoSeq' SELECT NEXT VALUE FOR dbo.DCenter_HoldingNoSeq AS [Value];
    ELSE IF @Sequence = N'DCenter_StockCountSeq' SELECT NEXT VALUE FOR dbo.DCenter_StockCountSeq AS [Value];
    ELSE THROW 50002, N'Unknown sequence.', 1;
END
GO

-- Live stock per lot and stage (voided lines and void entries excluded).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_LotStages
    @ByItems BIT,
    @ItemIds dbo.TT_DCenter_IdList READONLY,
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.[LotId], l.[ItemId],
           SUM((CASE WHEN m.[ToStage] = N'Normal' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Normal' THEN m.[QuantityKg] ELSE 0 END)) AS [NormalKg],
           SUM((CASE WHEN m.[ToStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END)) AS [BakingKg],
           SUM((CASE WHEN m.[ToStage] = N'Activated' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Activated' THEN m.[QuantityKg] ELSE 0 END)) AS [ActivatedKg]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'
      AND (@ByItems = 0 OR l.[ItemId] IN (SELECT [Id] FROM @ItemIds))
      AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY m.[LotId], l.[ItemId]
    ORDER BY m.[LotId]
    OPTION (RECOMPILE);
END
GO

-- Activated stock per lot and compartment (NULL compartment = unassigned / rack).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_ActivatedBins
    @ByItems BIT,
    @ItemIds dbo.TT_DCenter_IdList READONLY,
    @Category NVARCHAR(30),
    @AnyCompartment BIT,
    @CompartmentId INT
AS
BEGIN
    SET NOCOUNT ON;
    WITH Live AS
    (
        SELECT m.[LotId], l.[ItemId], m.[QuantityKg], m.[FromStage], m.[ToStage], m.[FromCompartmentId], m.[ToCompartmentId]
        FROM dbo.DCenter_ConsumableMovements m
        JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
        JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
        WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'
          AND (@ByItems = 0 OR l.[ItemId] IN (SELECT [Id] FROM @ItemIds))
      AND (@Category IS NULL OR i.[Category] = @Category)
          AND (@AnyCompartment = 0 OR m.[FromCompartmentId] IS NOT NULL OR m.[ToCompartmentId] IS NOT NULL)
          AND (@CompartmentId IS NULL OR m.[FromCompartmentId] = @CompartmentId OR m.[ToCompartmentId] = @CompartmentId)
    ),
    Flows AS
    (
        SELECT [LotId], [ItemId], [ToCompartmentId] AS [CompartmentId], SUM([QuantityKg]) AS [Kg]
        FROM Live WHERE [ToStage] = N'Activated'
        GROUP BY [LotId], [ItemId], [ToCompartmentId]
        UNION ALL
        SELECT [LotId], [ItemId], [FromCompartmentId], -SUM([QuantityKg])
        FROM Live WHERE [FromStage] = N'Activated'
        GROUP BY [LotId], [ItemId], [FromCompartmentId]
    )
    SELECT [LotId], [ItemId], [CompartmentId], SUM([Kg]) AS [Kg]
    FROM Flows
    GROUP BY [LotId], [ItemId], [CompartmentId]
    HAVING SUM([Kg]) <> 0
    ORDER BY [LotId], [ItemId], [CompartmentId]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_BakingBalances
    @Ids dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.[BakingRecordId] AS [Id], SUM((CASE WHEN m.[ToStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END)) AS [Kg]
    FROM dbo.DCenter_ConsumableMovements m
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[BakingRecordId] IN (SELECT [Id] FROM @Ids)
    GROUP BY m.[BakingRecordId];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_BakingFacts
    @Ids dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.[BakingRecordId] AS [Id],
           SUM(CASE WHEN m.[TxnType] = N'SendToBake' THEN 1 ELSE 0 END) AS [Sent],
           SUM(CASE WHEN m.[TxnType] = N'Return' AND m.[ToStage] = N'Baking' THEN 1 ELSE 0 END) AS [Rebake],
           SUM((CASE WHEN m.[ToStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END)) AS [Balance]
    FROM dbo.DCenter_ConsumableMovements m
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[BakingRecordId] IN (SELECT [Id] FROM @Ids)
    GROUP BY m.[BakingRecordId];
END
GO

-- Re-bake and placement facts for one baking record.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_BakingFlags
    @BakingRecordId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableMovements m WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'
            AND m.[BakingRecordId] = @BakingRecordId AND m.[TxnType] = N'Return' AND m.[ToStage] = N'Baking') THEN 1 ELSE 0 END AS BIT) AS [RebakeReturned],
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableMovements m WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'
            AND m.[BakingRecordId] = @BakingRecordId AND m.[FromStage] = N'Baking') THEN 1 ELSE 0 END AS BIT) AS [Placed],
        (SELECT SUM(m.[QuantityKg]) FROM dbo.DCenter_ConsumableMovements m WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'
            AND m.[BakingRecordId] = @BakingRecordId AND m.[FromStage] = N'Baking' AND m.[ToStage] = N'Activated') AS [IssuedToActivatedKg];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_RecentQuantities
    @ItemId INT,
    @TxnType NVARCHAR(20),
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Take) m.[QuantityKg] AS [Value]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[TxnType] = @TxnType AND l.[ItemId] = @ItemId
    ORDER BY m.[Id] DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_LotFlows
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.[LotId],
           SUM(CASE WHEN m.[TxnType] = N'Receive' THEN m.[QuantityKg] ELSE 0 END) AS [Received],
           SUM(CASE WHEN m.[TxnType] = N'Issue' OR m.[TxnType] = N'Finish' THEN m.[QuantityKg]
                    WHEN m.[TxnType] = N'Return' THEN -m.[QuantityKg] ELSE 0 END) AS [Taken],
           SUM((CASE WHEN m.[ToStage] = N'Normal' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Normal' THEN m.[QuantityKg] ELSE 0 END)) AS [NormalKg],
           SUM((CASE WHEN m.[ToStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END)) AS [BakingKg],
           SUM((CASE WHEN m.[ToStage] = N'Activated' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Activated' THEN m.[QuantityKg] ELSE 0 END)) AS [ActivatedKg]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY m.[LotId]
    OPTION (RECOMPILE);
END
GO

-- What a welder picked and returned per consumable since a date, with the last issue from Activated storage.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_WelderWindow
    @WelderId INT,
    @Since DATE,
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.[ItemId],
           SUM(CASE WHEN m.[TxnType] = N'Issue' THEN m.[QuantityKg] ELSE 0 END) AS [Picked],
           SUM(CASE WHEN m.[TxnType] = N'Return' THEN m.[QuantityKg] ELSE 0 END) AS [Returned],
           MAX(CASE WHEN m.[TxnType] = N'Issue' AND m.[FromStage] = N'Activated' THEN m.[Id] END) AS [LastIssueId]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[WelderId] = @WelderId AND m.[TxnDate] >= @Since
      AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY l.[ItemId]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_MonthlyFlows
    @From DATE,
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT YEAR(m.[TxnDate]) AS [Year], MONTH(m.[TxnDate]) AS [Month], m.[TxnType], i.[Category], SUM(m.[QuantityKg]) AS [Kg]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[TxnDate] >= @From
      AND m.[TxnType] IN (N'Receive', N'Issue', N'Return', N'Finish', N'Adjust')
      AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY YEAR(m.[TxnDate]), MONTH(m.[TxnDate]), m.[TxnType], i.[Category]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_ItemConsumption
    @Start DATE,
    @End DATE,
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.[ItemId], i.[Category], i.[Diameter], i.[Specification],
           SUM(CASE WHEN m.[TxnType] = N'Issue' THEN m.[QuantityKg] ELSE 0 END) AS [Picked],
           SUM(CASE WHEN m.[TxnType] = N'Return' THEN m.[QuantityKg] ELSE 0 END) AS [Returned],
           SUM(CASE WHEN m.[TxnType] = N'Finish' THEN m.[QuantityKg] ELSE 0 END) AS [Finished]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[TxnDate] >= @Start AND m.[TxnDate] < @End
      AND m.[TxnType] IN (N'Issue', N'Return', N'Finish')
      AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY l.[ItemId], i.[Category], i.[Diameter], i.[Specification]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_ItemMonthlyUse
    @From DATE,
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.[ItemId], YEAR(m.[TxnDate]) AS [Year], MONTH(m.[TxnDate]) AS [Month],
           SUM(CASE WHEN m.[TxnType] = N'Return' THEN -m.[QuantityKg] ELSE m.[QuantityKg] END) AS [Kg]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[TxnDate] >= @From
      AND m.[TxnType] IN (N'Issue', N'Return', N'Finish')
      AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY l.[ItemId], YEAR(m.[TxnDate]), MONTH(m.[TxnDate])
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_LastIssued
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.[ItemId], MAX(m.[TxnDate]) AS [Last]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[TxnType] = N'Issue' AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY l.[ItemId]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_LastIssuedLot
    @WelderId INT,
    @ItemId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) m.[LotId] AS [Value]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[TxnType] = N'Issue' AND m.[WelderId] = @WelderId AND l.[ItemId] = @ItemId
    ORDER BY m.[Id] DESC;
END
GO

-- Issued minus returned for a welder and consumable within the return window.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_Outstanding
    @WelderId INT,
    @ItemId INT,
    @Since DATE,
    @Until DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT SUM(CASE WHEN m.[TxnType] = N'Issue' THEN m.[QuantityKg] ELSE -m.[QuantityKg] END) AS [Value]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[WelderId] = @WelderId AND l.[ItemId] = @ItemId
      AND m.[TxnDate] >= @Since AND m.[TxnDate] <= @Until
      AND m.[TxnType] IN (N'Issue', N'Return');
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_BinSince
    @LotIds dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.[ToCompartmentId] AS [CompartmentId], m.[LotId], MAX(m.[CreatedAt]) AS [Last]
    FROM dbo.DCenter_ConsumableMovements m
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[ToStage] = N'Activated' AND m.[ToCompartmentId] IS NOT NULL
      AND m.[LotId] IN (SELECT [Id] FROM @LotIds)
    GROUP BY m.[ToCompartmentId], m.[LotId];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Ledger_OpeningReceipts
    @ReferenceNo NVARCHAR(60)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.[TxnNo], m.[TxnDate], m.[QuantityKg], m.[ToStage], m.[ToCompartmentId],
           i.[Specification], i.[Diameter], l.[Brand], l.[LotNumber]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[TxnType] = N'Receive' AND m.[ReferenceNo] = @ReferenceNo
    ORDER BY m.[Id];
END
GO

-- The first live receipt of each lot (date, source, received by).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lot_FirstReceipts
    @ItemId INT,
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT x.[LotId], x.[TxnDate], x.[Source], x.[Requestor]
    FROM
    (
        SELECT m.[LotId], m.[TxnDate], m.[Source], m.[Requestor],
               ROW_NUMBER() OVER (PARTITION BY m.[LotId] ORDER BY m.[TxnDate], m.[Id]) AS [N]
        FROM dbo.DCenter_ConsumableMovements m
        JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
        JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
        WHERE m.[IsVoided] = 0 AND m.[TxnType] = N'Receive'
          AND (@ItemId IS NULL OR l.[ItemId] = @ItemId)
          AND (@Category IS NULL OR i.[Category] = @Category)
    ) x
    WHERE x.[N] = 1
    OPTION (RECOMPILE);
END
GO

-- Transaction lines for the history, today and result views. @Sort: 1 Id, 2 Id desc, 3 newest first, 4 type/spec/lot.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Movement_List
    @TxnNo NVARCHAR(20),
    @WelderId INT,
    @CreatedFrom DATETIME2,
    @TxnType NVARCHAR(20),
    @ExcludeVoidEntries BIT,
    @From DATE,
    @To DATE,
    @Stage NVARCHAR(20),
    @Category NVARCHAR(30),
    @ItemId INT,
    @LotId INT,
    @CompartmentId INT,
    @BakingRecordId INT,
    @Terms dbo.TT_DCenter_TextList READONLY,
    @Sort INT,
    @Skip INT,
    @Take INT,
    @Total INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @Total = COUNT(*)
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE (@TxnNo IS NULL OR m.[TxnNo] = @TxnNo)
      AND (@WelderId IS NULL OR m.[WelderId] = @WelderId)
      AND (@CreatedFrom IS NULL OR m.[CreatedAt] >= @CreatedFrom)
      AND (@TxnType IS NULL OR m.[TxnType] = @TxnType)
      AND (@ExcludeVoidEntries = 0 OR m.[TxnType] <> N'Void')
      AND (@From IS NULL OR m.[TxnDate] >= @From)
      AND (@To IS NULL OR m.[TxnDate] <= @To)
      AND (@Stage IS NULL OR m.[FromStage] = @Stage OR m.[ToStage] = @Stage)
      AND (@Category IS NULL OR i.[Category] = @Category)
      AND (@ItemId IS NULL OR l.[ItemId] = @ItemId)
      AND (@LotId IS NULL OR m.[LotId] = @LotId)
      AND (@CompartmentId IS NULL OR m.[FromCompartmentId] = @CompartmentId OR m.[ToCompartmentId] = @CompartmentId)
      AND (@BakingRecordId IS NULL OR m.[BakingRecordId] = @BakingRecordId)
      AND (SELECT COUNT(*) FROM @Terms t WHERE CHARINDEX(t.[Value], m.[TxnNo]) > 0 OR CHARINDEX(t.[Value], l.[Brand]) > 0 OR CHARINDEX(t.[Value], l.[LotNumber]) > 0 OR CHARINDEX(t.[Value], i.[Specification]) > 0 OR CHARINDEX(t.[Value], i.[Diameter]) > 0 OR CHARINDEX(t.[Value], m.[Requestor]) > 0 OR CHARINDEX(t.[Value], m.[Remarks]) > 0 OR CHARINDEX(t.[Value], m.[CreatedBy]) > 0) = (SELECT COUNT(*) FROM @Terms)
    OPTION (RECOMPILE);

    SELECT m.[Id], m.[TxnNo], m.[TxnType], m.[TxnDate], m.[CreatedAt], m.[CreatedBy],
           l.[ItemId], i.[Category], i.[Specification], i.[Diameter], i.[Diameter] + N' ' + i.[Specification] AS [DiaSpec],
           m.[LotId], l.[Brand], l.[LotNumber], m.[QuantityKg], m.[FromStage], m.[ToStage],
           m.[Source], m.[Requestor], m.[WelderId], w.[WelderName], m.[Reason], m.[CountedQtyKg],
           m.[ReferenceNo], m.[Remarks], m.[IsVoided], m.[VoidsMovementId],
           m.[FromCompartmentId], fo.[Code] + N'-' + fc.[Label] AS [FromCompartment],
           m.[ToCompartmentId], tov.[Code] + N'-' + tc.[Label] AS [ToCompartment],
           m.[BakingRecordId], b.[BakingNo]
    FROM dbo.DCenter_ConsumableMovements m
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    LEFT JOIN dbo.DCenter_Welders w ON w.[Id] = m.[WelderId]
    LEFT JOIN dbo.DCenter_OvenCompartments fc ON fc.[Id] = m.[FromCompartmentId]
    LEFT JOIN dbo.DCenter_Ovens fo ON fo.[Id] = fc.[OvenId]
    LEFT JOIN dbo.DCenter_OvenCompartments tc ON tc.[Id] = m.[ToCompartmentId]
    LEFT JOIN dbo.DCenter_Ovens tov ON tov.[Id] = tc.[OvenId]
    LEFT JOIN dbo.DCenter_BakingRecords b ON b.[Id] = m.[BakingRecordId]
    WHERE (@TxnNo IS NULL OR m.[TxnNo] = @TxnNo)
      AND (@WelderId IS NULL OR m.[WelderId] = @WelderId)
      AND (@CreatedFrom IS NULL OR m.[CreatedAt] >= @CreatedFrom)
      AND (@TxnType IS NULL OR m.[TxnType] = @TxnType)
      AND (@ExcludeVoidEntries = 0 OR m.[TxnType] <> N'Void')
      AND (@From IS NULL OR m.[TxnDate] >= @From)
      AND (@To IS NULL OR m.[TxnDate] <= @To)
      AND (@Stage IS NULL OR m.[FromStage] = @Stage OR m.[ToStage] = @Stage)
      AND (@Category IS NULL OR i.[Category] = @Category)
      AND (@ItemId IS NULL OR l.[ItemId] = @ItemId)
      AND (@LotId IS NULL OR m.[LotId] = @LotId)
      AND (@CompartmentId IS NULL OR m.[FromCompartmentId] = @CompartmentId OR m.[ToCompartmentId] = @CompartmentId)
      AND (@BakingRecordId IS NULL OR m.[BakingRecordId] = @BakingRecordId)
      AND (SELECT COUNT(*) FROM @Terms t WHERE CHARINDEX(t.[Value], m.[TxnNo]) > 0 OR CHARINDEX(t.[Value], l.[Brand]) > 0 OR CHARINDEX(t.[Value], l.[LotNumber]) > 0 OR CHARINDEX(t.[Value], i.[Specification]) > 0 OR CHARINDEX(t.[Value], i.[Diameter]) > 0 OR CHARINDEX(t.[Value], m.[Requestor]) > 0 OR CHARINDEX(t.[Value], m.[Remarks]) > 0 OR CHARINDEX(t.[Value], m.[CreatedBy]) > 0) = (SELECT COUNT(*) FROM @Terms)
    ORDER BY
        CASE WHEN @Sort = 1 THEN m.[Id] END ASC,
        CASE WHEN @Sort = 2 THEN m.[Id] END DESC,
        CASE WHEN @Sort = 3 THEN m.[CreatedAt] END DESC,
        CASE WHEN @Sort = 3 THEN m.[Id] END DESC,
        CASE WHEN @Sort = 4 THEN i.[Category] END,
        CASE WHEN @Sort = 4 THEN i.[Specification] END,
        CASE WHEN @Sort = 4 THEN l.[LotNumber] END,
        CASE WHEN @Sort = 4 THEN m.[Id] END
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Movement_Get
    @TxnNo NVARCHAR(20),
    @NotVoidedOnly BIT,
    @ByIds BIT,
    @Ids dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [TxnNo], [TxnType], [TxnDate], [LotId], [QuantityKg], [FromStage], [ToStage], [FromCompartmentId], [ToCompartmentId], [BakingRecordId], [Source], [Requestor], [WelderId], [Reason], [CountedQtyKg], [ReferenceNo], [Remarks], [IsVoided], [VoidsMovementId], [CreatedBy], [CreatedAt]
    FROM dbo.DCenter_ConsumableMovements
    WHERE (@TxnNo IS NULL OR [TxnNo] = @TxnNo)
      AND (@NotVoidedOnly = 0 OR [IsVoided] = 0)
      AND (@ByIds = 0 OR [Id] IN (SELECT [Id] FROM @Ids))
    ORDER BY [Id]
    OPTION (RECOMPILE);
END
GO

-- Adds ledger lines in Seq order, so their Ids follow the order the lines were built in.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Movement_Insert
    @Rows dbo.TT_DCenter_MovementRows READONLY
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.DCenter_ConsumableMovements ([TxnNo], [TxnType], [TxnDate], [LotId], [QuantityKg], [FromStage], [ToStage], [FromCompartmentId], [ToCompartmentId], [BakingRecordId], [Source], [Requestor], [WelderId], [Reason], [CountedQtyKg], [ReferenceNo], [Remarks], [IsVoided], [VoidsMovementId], [CreatedBy], [CreatedAt])
    SELECT [TxnNo], [TxnType], [TxnDate], [LotId], [QuantityKg], [FromStage], [ToStage], [FromCompartmentId], [ToCompartmentId], [BakingRecordId], [Source], [Requestor], [WelderId], [Reason], [CountedQtyKg], [ReferenceNo], [Remarks], [IsVoided], [VoidsMovementId], [CreatedBy], [CreatedAt]
    FROM @Rows
    ORDER BY [Seq];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Movement_SetVoided
    @Ids dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE m SET [IsVoided] = 1
    FROM dbo.DCenter_ConsumableMovements m
    JOIN @Ids i ON i.[Id] = m.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Item_List
    @ByIds BIT,
    @Ids dbo.TT_DCenter_IdList READONLY,
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Category], [Specification], [Diameter], [MinStockKg], [ActivatedMinKg], [FinishThresholdKg], [HoldingOvenType], [IsActive], [CreatedAt]
    FROM dbo.DCenter_ConsumableItems
    WHERE (@ByIds = 0 OR [Id] IN (SELECT [Id] FROM @Ids))
      AND (@Category IS NULL OR [Category] = @Category)
    ORDER BY [Id]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Item_Search
    @Terms dbo.TT_DCenter_TextList READONLY,
    @Category NVARCHAR(30),
    @ActiveOnly BIT,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Take) [Id], [Category], [Specification], [Diameter], [MinStockKg], [ActivatedMinKg], [FinishThresholdKg], [HoldingOvenType], [IsActive], [CreatedAt]
    FROM dbo.DCenter_ConsumableItems i
    WHERE (@ActiveOnly = 0 OR i.[IsActive] = 1)
      AND (@Category IS NULL OR i.[Category] = @Category)
      AND (SELECT COUNT(*) FROM @Terms t WHERE CHARINDEX(t.[Value], i.[Specification]) > 0 OR CHARINDEX(t.[Value], i.[Diameter]) > 0 OR CHARINDEX(t.[Value], i.[Category]) > 0) = (SELECT COUNT(*) FROM @Terms)
    ORDER BY i.[Category], i.[Specification], i.[Diameter], i.[Id]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Item_FindBySpec
    @Specification NVARCHAR(100),
    @Diameter NVARCHAR(30),
    @ExcludeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) [Id], [Category], [Specification], [Diameter], [MinStockKg], [ActivatedMinKg], [FinishThresholdKg], [HoldingOvenType], [IsActive], [CreatedAt]
    FROM dbo.DCenter_ConsumableItems
    WHERE [Specification] = @Specification AND [Diameter] = @Diameter AND [Id] <> @ExcludeId;
END
GO

-- @Id NULL inserts a consumable; otherwise updates it. Returns the Id.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Item_Save
    @Id INT,
    @Category NVARCHAR(30),
    @Specification NVARCHAR(100),
    @Diameter NVARCHAR(30),
    @MinStockKg DECIMAL(10, 2),
    @ActivatedMinKg DECIMAL(10, 2),
    @FinishThresholdKg DECIMAL(10, 2),
    @HoldingOvenType NVARCHAR(30),
    @IsActive BIT,
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    IF @Id IS NULL
    BEGIN
        INSERT INTO dbo.DCenter_ConsumableItems
            ([Category], [Specification], [Diameter], [MinStockKg], [ActivatedMinKg], [FinishThresholdKg], [HoldingOvenType], [IsActive], [CreatedAt])
        VALUES (@Category, @Specification, @Diameter, @MinStockKg, @ActivatedMinKg, @FinishThresholdKg, @HoldingOvenType, @IsActive, @CreatedAt);
        SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Value];
        RETURN;
    END

    UPDATE dbo.DCenter_ConsumableItems
    SET [Category] = @Category, [Specification] = @Specification, [Diameter] = @Diameter, [MinStockKg] = @MinStockKg,
        [ActivatedMinKg] = @ActivatedMinKg, [FinishThresholdKg] = @FinishThresholdKg, [HoldingOvenType] = @HoldingOvenType,
        [IsActive] = @IsActive
    WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The consumable was changed or deleted by someone else.', 1;
    SELECT @Id AS [Value];
END
GO

-- Deletes a consumable and its lots (the caller checks there is no stock history).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Item_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DELETE FROM dbo.DCenter_ConsumableItemLots WHERE [ItemId] = @Id;
    DELETE FROM dbo.DCenter_ConsumableItems WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The consumable was changed or deleted by someone else.', 1;
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Item_History
    @ItemId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(*) FROM dbo.DCenter_ConsumableMovements m
         JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId] WHERE l.[ItemId] = @ItemId) AS [Movements],
        (SELECT COUNT(*) FROM dbo.DCenter_BakingRecords b
         JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId] WHERE l.[ItemId] = @ItemId) AS [Bakings];
END
GO

-- Consumables with at least one ledger line.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Item_StockedIds
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT l.[ItemId] AS [Value]
    FROM dbo.DCenter_ConsumableItemLots l
    WHERE EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableMovements m WHERE m.[LotId] = l.[Id]);
END
GO

-- Specification spellings: the dropdown list first (in its order), then the ones already used by consumables.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Item_SpecificationNames
    @LookupCategory NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Value]
    FROM
    (
        SELECT 0 AS [Src], [SortOrder], [Id], [Value] FROM dbo.DCenter_Lookups WHERE [Category] = @LookupCategory
        UNION ALL
        SELECT 1, 0, [Id], [Specification] FROM dbo.DCenter_ConsumableItems
    ) x
    ORDER BY [Src], [SortOrder], [Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lot_List
    @ByIds BIT,
    @Ids dbo.TT_DCenter_IdList READONLY,
    @ByItems BIT,
    @ItemIds dbo.TT_DCenter_IdList READONLY,
    @Category NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.[Id], l.[ItemId], l.[Brand], l.[LotNumber], l.[CreatedAt],
           i.[Category], i.[Specification], i.[Diameter], i.[HoldingOvenType], i.[MinStockKg], i.[IsActive]
    FROM dbo.DCenter_ConsumableItemLots l
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE (@ByIds = 0 OR l.[Id] IN (SELECT [Id] FROM @Ids))
      AND (@ByItems = 0 OR l.[ItemId] IN (SELECT [Id] FROM @ItemIds))
      AND (@Category IS NULL OR i.[Category] = @Category)
    ORDER BY l.[Id]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lot_Find
    @ItemId INT,
    @Brand NVARCHAR(100),
    @LotNumber NVARCHAR(60)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) [Id] AS [Value]
    FROM dbo.DCenter_ConsumableItemLots
    WHERE [ItemId] = @ItemId AND [Brand] = @Brand AND [LotNumber] = @LotNumber;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lot_Insert
    @ItemId INT,
    @Brand NVARCHAR(100),
    @LotNumber NVARCHAR(60),
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.DCenter_ConsumableItemLots ([ItemId], [Brand], [LotNumber], [CreatedAt])
    VALUES (@ItemId, @Brand, @LotNumber, @CreatedAt);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Value];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Oven_Board
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.[Id] AS [OvenId], o.[Name], o.[Code], o.[OvenType], c.[Id] AS [CompartmentId], c.[Number], c.[Label]
    FROM dbo.DCenter_Ovens o
    LEFT JOIN dbo.DCenter_OvenCompartments c ON c.[OvenId] = o.[Id]
    ORDER BY o.[Id], c.[Number];
END
GO

-- Compartments with their display label (oven code-label) and oven type.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Compartment_List
    @ByIds BIT,
    @Ids dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.[Id], o.[Code] + N'-' + c.[Label] AS [Label], o.[OvenType], c.[Number]
    FROM dbo.DCenter_OvenCompartments c
    JOIN dbo.DCenter_Ovens o ON o.[Id] = c.[OvenId]
    WHERE @ByIds = 0 OR c.[Id] IN (SELECT [Id] FROM @Ids)
    ORDER BY c.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Baking_Get
    @Ids dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [BakingNo], [LotId], [QuantityKg], [PersonInCharge], [BakingDate], [BakeStart], [BakeStop], [RebakeStart], [RebakeStop], [Status], [Remarks], [CreatedBy], [CreatedAt]
    FROM dbo.DCenter_BakingRecords
    WHERE [Id] IN (SELECT [Id] FROM @Ids)
    ORDER BY [Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Baking_Details
    @Ids dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.[Id], b.[BakingNo], l.[ItemId], i.[Category], i.[Diameter], i.[Specification], i.[HoldingOvenType],
           b.[LotId], l.[Brand], l.[LotNumber], b.[QuantityKg], b.[PersonInCharge], b.[BakingDate],
           b.[BakeStart], b.[BakeStop], b.[RebakeStart], b.[RebakeStop], b.[Status], b.[Remarks], b.[CreatedBy], b.[CreatedAt]
    FROM dbo.DCenter_BakingRecords b
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE b.[Id] IN (SELECT [Id] FROM @Ids)
    ORDER BY b.[Id];
END
GO

-- Baking record Ids (newest first) matching the filters, with the total count.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Baking_Search
    @From DATE,
    @To DATE,
    @Status NVARCHAR(20),
    @ByStatuses BIT,
    @Statuses dbo.TT_DCenter_TextList READONLY,
    @Terms dbo.TT_DCenter_TextList READONLY,
    @Skip INT,
    @Take INT,
    @Total INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Matches TABLE ([Id] INT NOT NULL PRIMARY KEY);
    INSERT INTO @Matches ([Id])
    SELECT b.[Id]
    FROM dbo.DCenter_BakingRecords b
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE (@From IS NULL OR b.[BakingDate] >= @From)
      AND (@To IS NULL OR b.[BakingDate] <= @To)
      AND (@Status IS NULL OR b.[Status] = @Status)
      AND (@ByStatuses = 0 OR b.[Status] IN (SELECT [Value] FROM @Statuses))
      AND (SELECT COUNT(*) FROM @Terms t WHERE CHARINDEX(t.[Value], b.[BakingNo]) > 0 OR CHARINDEX(t.[Value], b.[PersonInCharge]) > 0 OR CHARINDEX(t.[Value], l.[LotNumber]) > 0 OR CHARINDEX(t.[Value], l.[Brand]) > 0 OR CHARINDEX(t.[Value], i.[Specification]) > 0 OR CHARINDEX(t.[Value], i.[Diameter]) > 0) = (SELECT COUNT(*) FROM @Terms)
    OPTION (RECOMPILE);

    SELECT @Total = COUNT(*) FROM @Matches;
    SELECT [Id] AS [Value] FROM @Matches ORDER BY [Id] DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
END
GO

-- The baking record electrodes are returned to for re-baking: the requested one, or the lots latest finished bake.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Baking_ForRebake
    @LotId INT,
    @RequestedId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @RequestedId IS NOT NULL
        SELECT TOP (1) [Id], [BakingNo], [LotId], [QuantityKg], [PersonInCharge], [BakingDate], [BakeStart], [BakeStop], [RebakeStart], [RebakeStop], [Status], [Remarks], [CreatedBy], [CreatedAt]
        FROM dbo.DCenter_BakingRecords
        WHERE [Id] = @RequestedId AND [LotId] = @LotId AND [Status] <> N'Cancelled';
    ELSE
        SELECT TOP (1) [Id], [BakingNo], [LotId], [QuantityKg], [PersonInCharge], [BakingDate], [BakeStart], [BakeStop], [RebakeStart], [RebakeStop], [Status], [Remarks], [CreatedBy], [CreatedAt]
        FROM dbo.DCenter_BakingRecords
        WHERE [LotId] = @LotId AND [BakeStop] IS NOT NULL AND [Status] <> N'Cancelled'
        ORDER BY [Id] DESC;
END
GO

-- Adds baking records in Seq order and returns their Ids in the same order.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Baking_Insert
    @Rows dbo.TT_DCenter_BakingRows READONLY
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL PRIMARY KEY);
    INSERT INTO dbo.DCenter_BakingRecords
        ([BakingNo], [LotId], [QuantityKg], [PersonInCharge], [BakingDate], [Status], [Remarks], [CreatedBy], [CreatedAt])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [BakingNo], [LotId], [QuantityKg], [PersonInCharge], [BakingDate], [Status], [Remarks], [CreatedBy], [CreatedAt]
    FROM @Rows
    ORDER BY [Seq];
    SELECT [Id] AS [Value] FROM @Ids ORDER BY [Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Baking_Update
    @Id INT,
    @PersonInCharge NVARCHAR(100),
    @BakingDate DATE,
    @BakeStart DATETIME2,
    @BakeStop DATETIME2,
    @RebakeStart DATETIME2,
    @RebakeStop DATETIME2,
    @Remarks NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.DCenter_BakingRecords
    SET [PersonInCharge] = @PersonInCharge, [BakingDate] = @BakingDate, [BakeStart] = @BakeStart, [BakeStop] = @BakeStop,
        [RebakeStart] = @RebakeStart, [RebakeStop] = @RebakeStop, [Remarks] = @Remarks
    WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The baking record was changed or deleted by someone else.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Baking_SetTimes
    @Rows dbo.TT_DCenter_BakingTimes READONLY
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE b
    SET [BakeStart] = r.[BakeStart], [BakeStop] = r.[BakeStop], [RebakeStart] = r.[RebakeStart], [RebakeStop] = r.[RebakeStop]
    FROM dbo.DCenter_BakingRecords b
    JOIN @Rows r ON r.[Id] = b.[Id];
    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rows)
        THROW 50001, N'A baking record was changed or deleted by someone else.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Baking_SetStatus
    @Rows dbo.TT_DCenter_IdStatus READONLY
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE b SET [Status] = r.[Status]
    FROM dbo.DCenter_BakingRecords b
    JOIN @Rows r ON r.[Id] = b.[Id];
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Holding_Insert
    @HoldingNo NVARCHAR(20),
    @HoldingDate DATE,
    @BakingRecordId INT,
    @WelderId INT,
    @WelderName NVARCHAR(200),
    @CompartmentId INT,
    @IsFinishedAfterBaking BIT,
    @QuantityKg DECIMAL(10, 2),
    @TxnNo NVARCHAR(20),
    @Remarks NVARCHAR(500),
    @CreatedBy NVARCHAR(100),
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.DCenter_HoldingRecords
        ([HoldingNo], [HoldingDate], [BakingRecordId], [WelderId], [WelderName], [CompartmentId], [IsFinishedAfterBaking],
         [QuantityKg], [TxnNo], [IsVoided], [Remarks], [CreatedBy], [CreatedAt])
    VALUES (@HoldingNo, @HoldingDate, @BakingRecordId, @WelderId, @WelderName, @CompartmentId, @IsFinishedAfterBaking,
            @QuantityKg, @TxnNo, 0, @Remarks, @CreatedBy, @CreatedAt);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Value];
END
GO

-- Holding records (newest first) matching the filters, with the total count.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Holding_Search
    @Id INT,
    @From DATE,
    @To DATE,
    @Terms dbo.TT_DCenter_TextList READONLY,
    @Skip INT,
    @Take INT,
    @Total INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Matches TABLE ([Id] INT NOT NULL PRIMARY KEY);
    INSERT INTO @Matches ([Id])
    SELECT h.[Id]
    FROM dbo.DCenter_HoldingRecords h
    JOIN dbo.DCenter_BakingRecords b ON b.[Id] = h.[BakingRecordId]
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE (@Id IS NULL OR h.[Id] = @Id)
      AND (@From IS NULL OR h.[HoldingDate] >= @From)
      AND (@To IS NULL OR h.[HoldingDate] <= @To)
      AND (SELECT COUNT(*) FROM @Terms t WHERE CHARINDEX(t.[Value], h.[HoldingNo]) > 0 OR CHARINDEX(t.[Value], b.[BakingNo]) > 0 OR CHARINDEX(t.[Value], l.[LotNumber]) > 0 OR CHARINDEX(t.[Value], i.[Specification]) > 0 OR CHARINDEX(t.[Value], h.[WelderName]) > 0) = (SELECT COUNT(*) FROM @Terms)
    OPTION (RECOMPILE);

    SELECT @Total = COUNT(*) FROM @Matches;

    SELECT h.[Id], h.[HoldingNo], h.[HoldingDate], h.[BakingRecordId], b.[BakingNo], l.[ItemId],
           i.[Diameter] + N' ' + i.[Specification] AS [DiaSpec],
           b.[LotId], l.[Brand], l.[LotNumber], h.[WelderId], h.[WelderName],
           h.[CompartmentId], o.[Code] + N'-' + c.[Label] AS [CompartmentLabel], o.[OvenType], c.[Number] AS [CompartmentNumber],
           h.[IsFinishedAfterBaking], h.[QuantityKg], h.[TxnNo], h.[IsVoided], h.[Remarks], h.[CreatedBy], h.[CreatedAt]
    FROM dbo.DCenter_HoldingRecords h
    JOIN (SELECT [Id] FROM @Matches ORDER BY [Id] DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY) p ON p.[Id] = h.[Id]
    JOIN dbo.DCenter_BakingRecords b ON b.[Id] = h.[BakingRecordId]
    JOIN dbo.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId]
    JOIN dbo.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    LEFT JOIN dbo.DCenter_OvenCompartments c ON c.[Id] = h.[CompartmentId]
    LEFT JOIN dbo.DCenter_Ovens o ON o.[Id] = c.[OvenId]
    ORDER BY h.[Id] DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Holding_VoidByTxnNo
    @TxnNo NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.DCenter_HoldingRecords SET [IsVoided] = 1 WHERE [TxnNo] = @TxnNo AND [IsVoided] = 0;
END
GO

-- Moves open holding records of the given lots to another compartment.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Holding_Relocate
    @FromCompartmentId INT,
    @ToCompartmentId INT,
    @LotIds dbo.TT_DCenter_IdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE h SET [CompartmentId] = @ToCompartmentId
    FROM dbo.DCenter_HoldingRecords h
    JOIN dbo.DCenter_BakingRecords b ON b.[Id] = h.[BakingRecordId]
    WHERE h.[IsVoided] = 0 AND h.[IsFinishedAfterBaking] = 0 AND h.[CompartmentId] = @FromCompartmentId
      AND b.[LotId] IN (SELECT [Id] FROM @LotIds);
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_DCenter_StockCount_Insert
    @ReferenceNo NVARCHAR(20),
    @CountDate DATE,
    @Scope NVARCHAR(20),
    @Category NVARCHAR(30),
    @LinesCounted INT,
    @LinesAdjusted INT,
    @GainKg DECIMAL(10, 2),
    @LossKg DECIMAL(10, 2),
    @TxnNo NVARCHAR(20),
    @Remarks NVARCHAR(500),
    @CreatedBy NVARCHAR(100),
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.DCenter_StockCounts
        ([ReferenceNo], [CountDate], [Scope], [Category], [LinesCounted], [LinesAdjusted], [GainKg], [LossKg], [TxnNo], [Remarks], [CreatedBy], [CreatedAt])
    VALUES (@ReferenceNo, @CountDate, @Scope, @Category, @LinesCounted, @LinesAdjusted, @GainKg, @LossKg, @TxnNo, @Remarks, @CreatedBy, @CreatedAt);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Value];
END
GO

-- Stock counts (newest first); IsVoided is set when the counts adjustment was voided.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_StockCount_Search
    @Id INT,
    @ReferenceNo NVARCHAR(20),
    @From DATE,
    @To DATE,
    @Scope NVARCHAR(20),
    @Skip INT,
    @Take INT,
    @Total INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @Total = COUNT(*)
    FROM dbo.DCenter_StockCounts c
    WHERE (@Id IS NULL OR c.[Id] = @Id)
      AND (@ReferenceNo IS NULL OR c.[ReferenceNo] = @ReferenceNo)
      AND (@From IS NULL OR c.[CountDate] >= @From)
      AND (@To IS NULL OR c.[CountDate] <= @To)
      AND (@Scope IS NULL OR c.[Scope] = @Scope)
    OPTION (RECOMPILE);

    SELECT c.[Id], c.[ReferenceNo], c.[CountDate], c.[Scope], c.[Category], c.[LinesCounted], c.[LinesAdjusted],
           c.[GainKg], c.[LossKg], c.[TxnNo],
           CAST(CASE WHEN c.[TxnNo] IS NOT NULL AND EXISTS (
               SELECT 1 FROM dbo.DCenter_ConsumableMovements m WHERE m.[TxnNo] = c.[TxnNo] AND m.[IsVoided] = 1)
               THEN 1 ELSE 0 END AS BIT) AS [IsVoided],
           c.[Remarks], c.[CreatedBy], c.[CreatedAt]
    FROM dbo.DCenter_StockCounts c
    WHERE (@Id IS NULL OR c.[Id] = @Id)
      AND (@ReferenceNo IS NULL OR c.[ReferenceNo] = @ReferenceNo)
      AND (@From IS NULL OR c.[CountDate] >= @From)
      AND (@To IS NULL OR c.[CountDate] <= @To)
      AND (@Scope IS NULL OR c.[Scope] = @Scope)
    ORDER BY c.[Id] DESC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
    OPTION (RECOMPILE);
END
GO
