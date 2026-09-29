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
