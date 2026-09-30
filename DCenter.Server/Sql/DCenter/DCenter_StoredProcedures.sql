/*
    DCenter stored procedures. Run this against the DCenter database (the DefaultConnection database)
    after DCenter_SourceViews.sql. Safe to re-run: re-run the whole file after every release that changes it.
    Procedures can be created before the EF migration moves the tables into [dcenter]; SQL Server resolves
    table names when a procedure runs.

    Everything DCenter owns lives in schema [dcenter], so the shared database's dbo schema is not touched.
    - dcenter.SP_DCenter_<Module>_<Action>   stored procedures; they read and write the dcenter.DCenter_* tables
                                          (views are only used over OracleBetsyDB)
    - Several rows go in as one JSON string (NVARCHAR(MAX)) read with OPENJSON, so no table types are needed.
      NULL id lists mean "no filter"; an empty JSON array means "nothing".

    A procedure that must update or delete a row that no longer exists raises THROW 50001; the API turns
    that into the same "changed by someone else" answer as before.

    Requires SQL Server 2017 or later and database compatibility level 130 or higher (OPENJSON).
    With sqlcmd, pass -I (QUOTED_IDENTIFIER on):
        sqlcmd -S <server> -d DCenter -I -b -i DCenter_StoredProcedures.sql
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF (SELECT [compatibility_level] FROM sys.databases WHERE [name] = DB_NAME()) < 130
    THROW 50003, N'DCenter needs database compatibility level 130 or higher (OPENJSON). Ask the DBA to raise it, then run this script again.', 1;
GO

IF SCHEMA_ID(N'dcenter') IS NULL EXEC (N'CREATE SCHEMA [dcenter] AUTHORIZATION [dbo]');
GO

/* ===== Settings: welders ===== */
-- Welders by Id, by number (excluding one Id), or by search text, ordered by name.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Welder_List
    @Id INT = NULL,
    @WelderNo NVARCHAR(50) = NULL,
    @ExcludeId INT = NULL,
    @Q NVARCHAR(4000) = NULL,
    @ActiveOnly BIT = 0,
    @StockOnly BIT = 0,
    @Take INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(@Take, 2147483647)) [Id], [WelderName], [WelderNo], [IsActive], [UsageScope]
    FROM dcenter.DCenter_Welders
    WHERE (@Id IS NULL OR [Id] = @Id)
      AND (@WelderNo IS NULL OR [WelderNo] = @WelderNo)
      AND (@ExcludeId IS NULL OR [Id] <> @ExcludeId)
      AND (@Q IS NULL OR CHARINDEX(@Q, [WelderName]) > 0 OR CHARINDEX(@Q, [WelderNo]) > 0)
      AND (@ActiveOnly = 0 OR [IsActive] = 1)
      AND (@StockOnly = 0 OR [UsageScope] = N'ReportAndStock')
    ORDER BY [WelderName]
    OPTION (RECOMPILE);
END
GO
-- @Id NULL adds a welder; otherwise updates it. Returns the saved row.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Welder_Save
    @Id INT,
    @WelderName NVARCHAR(200),
    @WelderNo NVARCHAR(50),
    @IsActive BIT,
    @UsageScope NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    IF @Id IS NULL
    BEGIN
        INSERT INTO dcenter.DCenter_Welders ([WelderName], [WelderNo], [IsActive], [UsageScope])
        VALUES (@WelderName, @WelderNo, @IsActive, @UsageScope);
        SET @Id = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dcenter.DCenter_Welders
        SET [WelderName] = @WelderName, [WelderNo] = @WelderNo, [IsActive] = @IsActive, [UsageScope] = @UsageScope
        WHERE [Id] = @Id;
        IF @@ROWCOUNT = 0
            THROW 50001, N'The welder was changed or deleted by someone else.', 1;
    END

    SELECT [Id], [WelderName], [WelderNo], [IsActive], [UsageScope] FROM dcenter.DCenter_Welders WHERE [Id] = @Id;
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Welder_SetScope
    @Ids NVARCHAR(MAX),
    @UsageScope NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dcenter.DCenter_Welders
    SET [UsageScope] = @UsageScope
    WHERE [Id] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@Ids));

    SELECT @@ROWCOUNT AS [Value];
END
GO
-- Returns 0 without deleting when the welder has consumable pickups, returns or holdings on record.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Welder_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dcenter.DCenter_ConsumableMovements WHERE [WelderId] = @Id)
       OR EXISTS (SELECT 1 FROM dcenter.DCenter_HoldingRecords WHERE [WelderId] = @Id)
    BEGIN
        SELECT CAST(0 AS BIT) AS [Value];
        RETURN;
    END

    DELETE FROM dcenter.DCenter_Welders WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The welder was changed or deleted by someone else.', 1;
    SELECT CAST(1 AS BIT) AS [Value];
END
GO

/* ===== Settings: dropdown lists ===== */
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Lookup_List
    @Category NVARCHAR(50) = NULL,
    @Id INT = NULL,
    @Value NVARCHAR(200) = NULL,
    @ExcludeId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Category], [Value], [SortOrder], [IsActive]
    FROM dcenter.DCenter_Lookups
    WHERE (@Category IS NULL OR [Category] = @Category)
      AND (@Id IS NULL OR [Id] = @Id)
      AND (@Value IS NULL OR [Value] = @Value)
      AND (@ExcludeId IS NULL OR [Id] <> @ExcludeId)
    ORDER BY [Category], [SortOrder], [Value]
    OPTION (RECOMPILE);
END
GO
-- @Rows: JSON array of {Seq, Id, Category, Value, SortOrder, IsActive}. Rows with an Id are updated, the others inserted in Seq order; returns the inserted rows.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Lookup_Save
    @Rows NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Rs TABLE ([Seq] INT NOT NULL PRIMARY KEY, [Id] INT NULL, [Category] NVARCHAR(50) NOT NULL, [Value] NVARCHAR(200) NOT NULL,
                       [SortOrder] INT NOT NULL, [IsActive] BIT NOT NULL);
    DECLARE @Ids TABLE ([Id] INT NOT NULL);
    INSERT INTO @Rs SELECT * FROM OPENJSON(@Rows) WITH ([Seq] INT, [Id] INT, [Category] NVARCHAR(50), [Value] NVARCHAR(200), [SortOrder] INT, [IsActive] BIT);

    BEGIN TRANSACTION;

    UPDATE l
    SET [Category] = r.[Category], [Value] = r.[Value], [SortOrder] = r.[SortOrder], [IsActive] = r.[IsActive]
    FROM dcenter.DCenter_Lookups l
    JOIN @Rs r ON r.[Id] = l.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rs WHERE [Id] IS NOT NULL)
        THROW 50001, N'A dropdown value was changed or deleted by someone else.', 1;

    INSERT INTO dcenter.DCenter_Lookups ([Category], [Value], [SortOrder], [IsActive])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [Category], [Value], [SortOrder], [IsActive] FROM @Rs WHERE [Id] IS NULL ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT l.[Id], l.[Category], l.[Value], l.[SortOrder], l.[IsActive] FROM dcenter.DCenter_Lookups l JOIN @Ids i ON i.[Id] = l.[Id] ORDER BY l.[Id];
END
GO
-- @Rows: JSON array of {Id, SortOrder}.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Lookup_Reorder
    @Rows NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE l
    SET [SortOrder] = r.[SortOrder]
    FROM dcenter.DCenter_Lookups l
    JOIN OPENJSON(@Rows) WITH ([Id] INT, [SortOrder] INT) r ON r.[Id] = l.[Id];
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Lookup_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dcenter.DCenter_Lookups WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The dropdown value was changed or deleted by someone else.', 1;
END
GO

/* ===== Settings: Process–Type links ===== */
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_ProcessTypeLink_List
    @Process NVARCHAR(200) = NULL,
    @Type NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Process], [Type]
    FROM dcenter.DCenter_ProcessTypeLinks
    WHERE (@Process IS NULL OR [Process] = @Process)
      AND (@Type IS NULL OR [Type] = @Type)
    ORDER BY [Process], [Type];
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_ProcessTypeLink_Insert
    @Process NVARCHAR(200),
    @Type NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dcenter.DCenter_ProcessTypeLinks ([Process], [Type]) VALUES (@Process, @Type);
    SELECT [Id], [Process], [Type] FROM dcenter.DCenter_ProcessTypeLinks WHERE [Id] = CAST(SCOPE_IDENTITY() AS INT);
END
GO
-- Returns the number of rows deleted (0 when the link no longer exists).
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_ProcessTypeLink_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dcenter.DCenter_ProcessTypeLinks WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [Value];
END
GO

/* ===== Settings: WPS ===== */
-- All rows, one row by Id, or the rows sharing the required key values.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Wps_List
    @Id INT = NULL,
    @WpsNo NVARCHAR(200) = NULL,
    @PNo NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WpsNo], [PNo], [BaseMetal], [Process]
    FROM dcenter.DCenter_WpsItems
    WHERE (@Id IS NULL OR [Id] = @Id)
      AND (@WpsNo IS NULL OR [WpsNo] = @WpsNo)
      AND (@PNo IS NULL OR [PNo] = @PNo)
    ORDER BY [WpsNo], [PNo]
    OPTION (RECOMPILE);
END
GO
-- @Rows: JSON array of [Seq, Id, WpsNo, PNo, BaseMetal, Process] arrays. Rows with an Id are updated, the others inserted in Seq order; returns the inserted rows.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Wps_Save
    @Rows NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Rs TABLE ([Seq] INT NOT NULL PRIMARY KEY, [Id] INT NULL, [WpsNo] NVARCHAR(200) NULL, [PNo] NVARCHAR(50) NULL, [BaseMetal] NVARCHAR(200) NULL, [Process] NVARCHAR(100) NULL);
    DECLARE @Ids TABLE ([Id] INT NOT NULL);
    INSERT INTO @Rs SELECT * FROM OPENJSON(@Rows) WITH ([Seq] INT '$[0]', [Id] INT '$[1]', [WpsNo] NVARCHAR(200) '$[2]', [PNo] NVARCHAR(50) '$[3]', [BaseMetal] NVARCHAR(200) '$[4]', [Process] NVARCHAR(100) '$[5]');

    BEGIN TRANSACTION;

    UPDATE t
    SET [WpsNo] = r.[WpsNo], [PNo] = r.[PNo], [BaseMetal] = r.[BaseMetal], [Process] = r.[Process]
    FROM dcenter.DCenter_WpsItems t
    JOIN @Rs r ON r.[Id] = t.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rs WHERE [Id] IS NOT NULL)
        THROW 50001, N'A WPS row was changed or deleted by someone else.', 1;

    INSERT INTO dcenter.DCenter_WpsItems ([WpsNo], [PNo], [BaseMetal], [Process])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [WpsNo], [PNo], [BaseMetal], [Process] FROM @Rs WHERE [Id] IS NULL ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT t.[Id], t.[WpsNo], t.[PNo], t.[BaseMetal], t.[Process] FROM dcenter.DCenter_WpsItems t JOIN @Ids i ON i.[Id] = t.[Id] ORDER BY t.[Id];
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Wps_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dcenter.DCenter_WpsItems WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The WPS row was changed or deleted by someone else.', 1;
END
GO

/* ===== Settings: MRN ===== */
-- All rows, one row by Id, or the rows sharing the required key values.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Mrn_List
    @Id INT = NULL,
    @Mrn NVARCHAR(100) = NULL,
    @SpecNo NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Mrn], [SpecNo], [Form], [FullSpecification]
    FROM dcenter.DCenter_MrnSpecs
    WHERE (@Id IS NULL OR [Id] = @Id)
      AND (@Mrn IS NULL OR [Mrn] = @Mrn)
      AND (@SpecNo IS NULL OR [SpecNo] = @SpecNo)
    ORDER BY [Mrn], [SpecNo]
    OPTION (RECOMPILE);
END
GO
-- @Rows: JSON array of [Seq, Id, Mrn, SpecNo, Form, FullSpecification] arrays. Rows with an Id are updated, the others inserted in Seq order; returns the inserted rows.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Mrn_Save
    @Rows NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Rs TABLE ([Seq] INT NOT NULL PRIMARY KEY, [Id] INT NULL, [Mrn] NVARCHAR(100) NULL, [SpecNo] NVARCHAR(100) NULL, [Form] NVARCHAR(200) NULL, [FullSpecification] NVARCHAR(400) NULL);
    DECLARE @Ids TABLE ([Id] INT NOT NULL);
    INSERT INTO @Rs SELECT * FROM OPENJSON(@Rows) WITH ([Seq] INT '$[0]', [Id] INT '$[1]', [Mrn] NVARCHAR(100) '$[2]', [SpecNo] NVARCHAR(100) '$[3]', [Form] NVARCHAR(200) '$[4]', [FullSpecification] NVARCHAR(400) '$[5]');

    BEGIN TRANSACTION;

    UPDATE t
    SET [Mrn] = r.[Mrn], [SpecNo] = r.[SpecNo], [Form] = r.[Form], [FullSpecification] = r.[FullSpecification]
    FROM dcenter.DCenter_MrnSpecs t
    JOIN @Rs r ON r.[Id] = t.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rs WHERE [Id] IS NOT NULL)
        THROW 50001, N'A MRN row was changed or deleted by someone else.', 1;

    INSERT INTO dcenter.DCenter_MrnSpecs ([Mrn], [SpecNo], [Form], [FullSpecification])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [Mrn], [SpecNo], [Form], [FullSpecification] FROM @Rs WHERE [Id] IS NULL ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT t.[Id], t.[Mrn], t.[SpecNo], t.[Form], t.[FullSpecification] FROM dcenter.DCenter_MrnSpecs t JOIN @Ids i ON i.[Id] = t.[Id] ORDER BY t.[Id];
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Mrn_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dcenter.DCenter_MrnSpecs WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The MRN row was changed or deleted by someone else.', 1;
END
GO

/* ===== Settings: BPVC IX ===== */
-- All rows, one row by Id, or the rows sharing the required key values.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Bpvc_List
    @Id INT = NULL,
    @SpecNo NVARCHAR(100) = NULL,
    @PNo NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM dcenter.DCenter_BpvcIx
    WHERE (@Id IS NULL OR [Id] = @Id)
      AND (@SpecNo IS NULL OR [SpecNo] = @SpecNo)
      AND (@PNo IS NULL OR [PNo] = @PNo)
    ORDER BY [SpecNo], [Designation], [UnsNo], [PNo]
    OPTION (RECOMPILE);
END
GO
-- @Rows: JSON array of [Seq, Id, SpecNo, Designation, UnsNo, PNo, MinTensile, GroupNo, IsoGroup, BrazingPNo, NominalComposition, TypicalProductForm, NominalThicknessLimits] arrays. Rows with an Id are updated, the others inserted in Seq order; returns the inserted rows.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Bpvc_Save
    @Rows NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Rs TABLE ([Seq] INT NOT NULL PRIMARY KEY, [Id] INT NULL, [SpecNo] NVARCHAR(100) NULL, [Designation] NVARCHAR(200) NULL, [UnsNo] NVARCHAR(100) NULL, [PNo] NVARCHAR(50) NULL, [MinTensile] NVARCHAR(100) NULL, [GroupNo] NVARCHAR(50) NULL, [IsoGroup] NVARCHAR(100) NULL, [BrazingPNo] NVARCHAR(50) NULL, [NominalComposition] NVARCHAR(400) NULL, [TypicalProductForm] NVARCHAR(200) NULL, [NominalThicknessLimits] NVARCHAR(200) NULL);
    DECLARE @Ids TABLE ([Id] INT NOT NULL);
    INSERT INTO @Rs SELECT * FROM OPENJSON(@Rows) WITH ([Seq] INT '$[0]', [Id] INT '$[1]', [SpecNo] NVARCHAR(100) '$[2]', [Designation] NVARCHAR(200) '$[3]', [UnsNo] NVARCHAR(100) '$[4]', [PNo] NVARCHAR(50) '$[5]', [MinTensile] NVARCHAR(100) '$[6]', [GroupNo] NVARCHAR(50) '$[7]', [IsoGroup] NVARCHAR(100) '$[8]', [BrazingPNo] NVARCHAR(50) '$[9]', [NominalComposition] NVARCHAR(400) '$[10]', [TypicalProductForm] NVARCHAR(200) '$[11]', [NominalThicknessLimits] NVARCHAR(200) '$[12]');

    BEGIN TRANSACTION;

    UPDATE t
    SET [SpecNo] = r.[SpecNo], [Designation] = r.[Designation], [UnsNo] = r.[UnsNo], [PNo] = r.[PNo], [MinTensile] = r.[MinTensile], [GroupNo] = r.[GroupNo], [IsoGroup] = r.[IsoGroup], [BrazingPNo] = r.[BrazingPNo], [NominalComposition] = r.[NominalComposition], [TypicalProductForm] = r.[TypicalProductForm], [NominalThicknessLimits] = r.[NominalThicknessLimits]
    FROM dcenter.DCenter_BpvcIx t
    JOIN @Rs r ON r.[Id] = t.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rs WHERE [Id] IS NOT NULL)
        THROW 50001, N'A BPVC row was changed or deleted by someone else.', 1;

    INSERT INTO dcenter.DCenter_BpvcIx ([SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits] FROM @Rs WHERE [Id] IS NULL ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT t.[Id], t.[SpecNo], t.[Designation], t.[UnsNo], t.[PNo], t.[MinTensile], t.[GroupNo], t.[IsoGroup], t.[BrazingPNo], t.[NominalComposition], t.[TypicalProductForm], t.[NominalThicknessLimits] FROM dcenter.DCenter_BpvcIx t JOIN @Ids i ON i.[Id] = t.[Id] ORDER BY t.[Id];
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Bpvc_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dcenter.DCenter_BpvcIx WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The BPVC row was changed or deleted by someone else.', 1;
END
GO

/* ===== Weld reports ===== */
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Report_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.[Id], r.[WorkOrderNumber], r.[PartNo], r.[Description],
           (SELECT COUNT(*) FROM dcenter.DCenter_Joints j WHERE j.[ReportId] = r.[Id]) AS [JointCount],
           CASE WHEN r.[CompletedAt] IS NULL THEN N'Draft' ELSE N'Completed' END AS [Status],
           r.[UpdatedAt]
    FROM dcenter.DCenter_Reports r
    ORDER BY r.[UpdatedAt] DESC;
END
GO
-- The report with its joints and electrode data as one JSON document (one statement, so one consistent version); NULL when there is no report.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Report_Get
    @WorkOrderNumber NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
    (
        SELECT r.[Id], r.[WorkOrderNumber], r.[ReportRequired], r.[DateWelded], r.[PartNo], r.[Description], r.[MaterialSpec1], r.[MaterialSpec2], r.[MaterialSpec3], r.[Grade1], r.[Grade2], r.[Grade3], r.[PNumber1], r.[PNumber2], r.[PNumber3], r.[EngineerSupervisor], r.[QaInspector],
               r.[CreatedAt], r.[UpdatedAt], r.[CompletedAt], r.[RowVersion],
               (
                   SELECT j.[Id], j.[ReportId], j.[JointNumber], j.[PartDescLeft], j.[PartNoLeft], j.[HeatNumberLeft], j.[PartDescRight], j.[PartNoRight], j.[HeatNumberRight], j.[WpsNo], j.[Rev], j.[WelderName], j.[WelderNo],
                          (
                              SELECT m.[Id], m.[JointId], m.[ColumnNumber], m.[Process], m.[Size], m.[Type], m.[Manuf], m.[HeatLot]
                              FROM dcenter.DCenter_JointMaterials m
                              WHERE m.[JointId] = j.[Id]
                              ORDER BY m.[Id]
                              FOR JSON PATH
                          ) AS [Materials]
                   FROM dcenter.DCenter_Joints j
                   WHERE j.[ReportId] = r.[Id]
                   ORDER BY j.[Id]
                   FOR JSON PATH
               ) AS [Joints]
        FROM dcenter.DCenter_Reports r
        WHERE r.[WorkOrderNumber] = @WorkOrderNumber
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS [Value];
END
GO
-- @Id NULL inserts a new report; otherwise it is updated only while its RowVersion still matches (THROW 50001 when it does not) and its joints are replaced. @Joints: JSON array of {Seq, JointNumber, ...}; @Materials: {JointSeq, Seq, ColumnNumber, ...}. Returns the report Id.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Report_Save
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
    @Joints NVARCHAR(MAX),
    @Materials NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Js TABLE ([Seq] INT NOT NULL PRIMARY KEY, [JointNumber] INT NOT NULL, [PartDescLeft] NVARCHAR(MAX) NULL, [PartNoLeft] NVARCHAR(MAX) NULL, [HeatNumberLeft] NVARCHAR(MAX) NULL, [PartDescRight] NVARCHAR(MAX) NULL, [PartNoRight] NVARCHAR(MAX) NULL, [HeatNumberRight] NVARCHAR(MAX) NULL, [WpsNo] NVARCHAR(MAX) NULL, [Rev] NVARCHAR(MAX) NULL, [WelderName] NVARCHAR(MAX) NULL, [WelderNo] NVARCHAR(MAX) NULL);
    DECLARE @JointIds TABLE ([Id] INT NOT NULL PRIMARY KEY);
    INSERT INTO @Js SELECT * FROM OPENJSON(@Joints) WITH ([Seq] INT, [JointNumber] INT, [PartDescLeft] NVARCHAR(MAX), [PartNoLeft] NVARCHAR(MAX), [HeatNumberLeft] NVARCHAR(MAX), [PartDescRight] NVARCHAR(MAX), [PartNoRight] NVARCHAR(MAX), [HeatNumberRight] NVARCHAR(MAX), [WpsNo] NVARCHAR(MAX), [Rev] NVARCHAR(MAX), [WelderName] NVARCHAR(MAX), [WelderNo] NVARCHAR(MAX));

    BEGIN TRANSACTION;

    IF @Id IS NULL
    BEGIN
        INSERT INTO dcenter.DCenter_Reports ([WorkOrderNumber], [ReportRequired], [DateWelded], [PartNo], [Description], [MaterialSpec1], [MaterialSpec2], [MaterialSpec3], [Grade1], [Grade2], [Grade3], [PNumber1], [PNumber2], [PNumber3], [EngineerSupervisor], [QaInspector], [CreatedAt], [UpdatedAt])
        VALUES (@WorkOrderNumber, @ReportRequired, @DateWelded, @PartNo, @Description, @MaterialSpec1, @MaterialSpec2, @MaterialSpec3, @Grade1, @Grade2, @Grade3, @PNumber1, @PNumber2, @PNumber3, @EngineerSupervisor, @QaInspector, @CreatedAt, @UpdatedAt);
        SET @Id = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dcenter.DCenter_Reports
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
        FROM dcenter.DCenter_JointMaterials m
        JOIN dcenter.DCenter_Joints j ON j.[Id] = m.[JointId]
        WHERE j.[ReportId] = @Id;

        DELETE FROM dcenter.DCenter_Joints WHERE [ReportId] = @Id;
    END

    INSERT INTO dcenter.DCenter_Joints ([ReportId], [JointNumber], [PartDescLeft], [PartNoLeft], [HeatNumberLeft], [PartDescRight], [PartNoRight], [HeatNumberRight], [WpsNo], [Rev], [WelderName], [WelderNo])
    OUTPUT inserted.[Id] INTO @JointIds
    SELECT @Id, [JointNumber], [PartDescLeft], [PartNoLeft], [HeatNumberLeft], [PartDescRight], [PartNoRight], [HeatNumberRight], [WpsNo], [Rev], [WelderName], [WelderNo]
    FROM @Js
    ORDER BY [Seq];

    -- Identity values follow the ORDER BY above, so the n-th new Id belongs to the n-th joint.
    WITH NewIds AS (SELECT [Id], ROW_NUMBER() OVER (ORDER BY [Id]) AS [N] FROM @JointIds),
         Seqs AS (SELECT [Seq], ROW_NUMBER() OVER (ORDER BY [Seq]) AS [N] FROM @Js)
    INSERT INTO dcenter.DCenter_JointMaterials ([JointId], [ColumnNumber], [Process], [Size], [Type], [Manuf], [HeatLot])
    SELECT n.[Id], m.[ColumnNumber], m.[Process], m.[Size], m.[Type], m.[Manuf], m.[HeatLot]
    FROM OPENJSON(@Materials) WITH ([JointSeq] INT, [Seq] INT, [ColumnNumber] INT, [Process] NVARCHAR(MAX), [Size] NVARCHAR(MAX), [Type] NVARCHAR(MAX), [Manuf] NVARCHAR(MAX), [HeatLot] NVARCHAR(MAX)) m
    JOIN Seqs s ON s.[Seq] = m.[JointSeq]
    JOIN NewIds n ON n.[N] = s.[N]
    ORDER BY m.[JointSeq], m.[Seq];

    INSERT INTO dcenter.DCenter_ReportStatusEvents ([ReportId], [Action], [OccurredAt], [Details])
    VALUES (@Id, @Action, @OccurredAt, @Details);

    COMMIT TRANSACTION;

    SELECT @Id AS [Value];
END
GO
-- Completes or reopens a report while its RowVersion still matches (THROW 50001 when it does not).
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Report_SetStatus
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

    UPDATE dcenter.DCenter_Reports
    SET [CompletedAt] = @CompletedAt, [UpdatedAt] = @UpdatedAt
    WHERE [Id] = @Id AND [RowVersion] = @RowVersion;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The report was changed by someone else.', 1;

    INSERT INTO dcenter.DCenter_ReportStatusEvents ([ReportId], [Action], [OccurredAt], [Details])
    VALUES (@Id, @Action, @OccurredAt, @Details);

    COMMIT TRANSACTION;
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Report_History
    @ReportId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Action], [OccurredAt], [Details]
    FROM dcenter.DCenter_ReportStatusEvents
    WHERE [ReportId] = @ReportId
    ORDER BY [OccurredAt] DESC;
END
GO
-- Joints, materials and status events go with the report (ON DELETE CASCADE).
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Report_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dcenter.DCenter_Reports WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The report was changed or deleted by someone else.', 1;
END
GO
-- Dashboard data as one JSON document: reports in the window, joints per welder, top WPS numbers.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Report_Dashboard
    @WindowStart DATE,
    @WindowStartAt DATETIME2,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
    (
        SELECT
            (
                SELECT r.[WorkOrderNumber], r.[PartNo], r.[Description], r.[CompletedAt], r.[UpdatedAt], r.[DateWelded],
                       (SELECT COUNT(*) FROM dcenter.DCenter_Joints j WHERE j.[ReportId] = r.[Id]) AS [JointCount]
                FROM dcenter.DCenter_Reports r
                WHERE r.[ReportRequired] = 1
                  AND (r.[CompletedAt] IS NULL OR r.[CompletedAt] >= @WindowStartAt OR r.[DateWelded] >= @WindowStart)
                FOR JSON PATH
            ) AS [Reports],
            (
                SELECT j.[WelderNo], j.[WelderName], COUNT(*) AS [Count]
                FROM dcenter.DCenter_Joints j
                JOIN dcenter.DCenter_Reports r ON r.[Id] = j.[ReportId]
                WHERE r.[ReportRequired] = 1 AND r.[DateWelded] >= @WindowStart
                  AND j.[WelderNo] IS NOT NULL AND j.[WelderNo] <> N''
                GROUP BY j.[WelderNo], j.[WelderName]
                FOR JSON PATH
            ) AS [Welders],
            (
                SELECT TOP (@Take) j.[WpsNo], COUNT(*) AS [Count]
                FROM dcenter.DCenter_Joints j
                JOIN dcenter.DCenter_Reports r ON r.[Id] = j.[ReportId]
                WHERE r.[ReportRequired] = 1 AND r.[DateWelded] >= @WindowStart
                  AND j.[WpsNo] IS NOT NULL AND j.[WpsNo] <> N''
                GROUP BY j.[WpsNo]
                ORDER BY COUNT(*) DESC
                FOR JSON PATH
            ) AS [Wps]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS [Value];
END
GO
-- @Field is welder, wps, heat or heatLot. HeatLots lists the joint's electrode heat/lots in column order, separated by CHAR(31).
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Report_Trace
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
            FROM dcenter.DCenter_JointMaterials m WHERE m.[JointId] = j.[Id]) AS [HeatLots]
    FROM dcenter.DCenter_Joints j
    JOIN dcenter.DCenter_Reports r ON r.[Id] = j.[ReportId]
    WHERE (@Field = N'welder' AND (CHARINDEX(@Q, j.[WelderNo]) > 0 OR CHARINDEX(@Q, j.[WelderName]) > 0))
       OR (@Field = N'wps' AND CHARINDEX(@Q, j.[WpsNo]) > 0)
       OR (@Field = N'heat' AND (CHARINDEX(@Q, j.[HeatNumberLeft]) > 0 OR CHARINDEX(@Q, j.[HeatNumberRight]) > 0))
       OR (@Field = N'heatLot' AND EXISTS (
            SELECT 1 FROM dcenter.DCenter_JointMaterials m WHERE m.[JointId] = j.[Id] AND CHARINDEX(@Q, m.[HeatLot]) > 0))
    ORDER BY r.[DateWelded] DESC, r.[WorkOrderNumber], j.[JointNumber];
END
GO

/* ===== Consumables: stock ledger ===== */
-- Next number from one of the fixed DCenter sequences.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Sequence_Next
    @Sequence NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    IF @Sequence = N'DCenter_ConsumableTxnSeq' SELECT NEXT VALUE FOR dcenter.DCenter_ConsumableTxnSeq AS [Value];
    ELSE IF @Sequence = N'DCenter_BakingNoSeq' SELECT NEXT VALUE FOR dcenter.DCenter_BakingNoSeq AS [Value];
    ELSE IF @Sequence = N'DCenter_HoldingNoSeq' SELECT NEXT VALUE FOR dcenter.DCenter_HoldingNoSeq AS [Value];
    ELSE IF @Sequence = N'DCenter_StockCountSeq' SELECT NEXT VALUE FOR dcenter.DCenter_StockCountSeq AS [Value];
    ELSE THROW 50002, N'Unknown sequence.', 1;
END
GO
-- Live stock per lot (by stage), received and taken totals, last issue date and first receipt. Voided lines and void entries are excluded.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Ledger_Lots
    @ItemIds NVARCHAR(MAX) = NULL,
    @Category NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    WITH Lots AS
    (
        SELECT m.[LotId], l.[ItemId],
               SUM((CASE WHEN m.[ToStage] = N'Normal' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Normal' THEN m.[QuantityKg] ELSE 0 END)) AS [NormalKg],
               SUM((CASE WHEN m.[ToStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END)) AS [BakingKg],
               SUM((CASE WHEN m.[ToStage] = N'Activated' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Activated' THEN m.[QuantityKg] ELSE 0 END)) AS [ActivatedKg],
               SUM(CASE WHEN m.[TxnType] = N'Receive' THEN m.[QuantityKg] ELSE 0 END) AS [ReceivedKg],
               SUM(CASE WHEN m.[TxnType] = N'Issue' OR m.[TxnType] = N'Finish' THEN m.[QuantityKg]
                        WHEN m.[TxnType] = N'Return' THEN -m.[QuantityKg] ELSE 0 END) AS [TakenKg],
               MAX(CASE WHEN m.[TxnType] = N'Issue' THEN m.[TxnDate] END) AS [LastIssuedOn]
        FROM dcenter.DCenter_ConsumableMovements m
        JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
        JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
        WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'
          AND (@ItemIds IS NULL OR l.[ItemId] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@ItemIds)))
          AND (@Category IS NULL OR i.[Category] = @Category)
        GROUP BY m.[LotId], l.[ItemId]
    )
    SELECT x.[LotId], x.[ItemId], x.[NormalKg], x.[BakingKg], x.[ActivatedKg], x.[ReceivedKg], x.[TakenKg], x.[LastIssuedOn],
           fr.[TxnDate] AS [FirstReceivedOn], fr.[Source] AS [FirstSource], fr.[Requestor] AS [FirstReceivedBy]
    FROM Lots x
    OUTER APPLY
    (
        SELECT TOP (1) r.[TxnDate], r.[Source], r.[Requestor]
        FROM dcenter.DCenter_ConsumableMovements r
        WHERE r.[LotId] = x.[LotId] AND r.[IsVoided] = 0 AND r.[TxnType] = N'Receive'
        ORDER BY r.[TxnDate], r.[Id]
    ) fr
    ORDER BY x.[LotId]
    OPTION (RECOMPILE);
END
GO
-- Activated stock per lot and compartment (NULL compartment = unassigned / rack), with when stock last went in.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Ledger_ActivatedBins
    @ItemIds NVARCHAR(MAX) = NULL,
    @Category NVARCHAR(30) = NULL,
    @AnyCompartment BIT = 0,
    @CompartmentId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    WITH Live AS
    (
        SELECT m.[LotId], l.[ItemId], m.[QuantityKg], m.[FromStage], m.[ToStage], m.[FromCompartmentId], m.[ToCompartmentId], m.[CreatedAt]
        FROM dcenter.DCenter_ConsumableMovements m
        JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
        JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
        WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'
          AND (@ItemIds IS NULL OR l.[ItemId] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@ItemIds)))
          AND (@Category IS NULL OR i.[Category] = @Category)
          AND (@AnyCompartment = 0 OR m.[FromCompartmentId] IS NOT NULL OR m.[ToCompartmentId] IS NOT NULL)
          AND (@CompartmentId IS NULL OR m.[FromCompartmentId] = @CompartmentId OR m.[ToCompartmentId] = @CompartmentId)
    ),
    Flows AS
    (
        SELECT [LotId], [ItemId], [ToCompartmentId] AS [CompartmentId], SUM([QuantityKg]) AS [Kg], MAX([CreatedAt]) AS [LastInAt]
        FROM Live WHERE [ToStage] = N'Activated'
        GROUP BY [LotId], [ItemId], [ToCompartmentId]
        UNION ALL
        SELECT [LotId], [ItemId], [FromCompartmentId], -SUM([QuantityKg]), NULL
        FROM Live WHERE [FromStage] = N'Activated'
        GROUP BY [LotId], [ItemId], [FromCompartmentId]
    )
    SELECT [LotId], [ItemId], [CompartmentId], SUM([Kg]) AS [Kg], MAX([LastInAt]) AS [LastInAt]
    FROM Flows
    GROUP BY [LotId], [ItemId], [CompartmentId]
    HAVING SUM([Kg]) <> 0
    ORDER BY [LotId], [ItemId], [CompartmentId]
    OPTION (RECOMPILE);
END
GO
-- Per baking record: sent, re-bake returns, placements, kg still in baking, kg placed into Activated storage.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Ledger_Baking
    @Ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.[BakingRecordId] AS [Id],
           SUM(CASE WHEN m.[TxnType] = N'SendToBake' THEN 1 ELSE 0 END) AS [Sent],
           SUM(CASE WHEN m.[TxnType] = N'Return' AND m.[ToStage] = N'Baking' THEN 1 ELSE 0 END) AS [Rebake],
           SUM(CASE WHEN m.[FromStage] = N'Baking' THEN 1 ELSE 0 END) AS [Placed],
           SUM((CASE WHEN m.[ToStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END) - (CASE WHEN m.[FromStage] = N'Baking' THEN m.[QuantityKg] ELSE 0 END)) AS [BalanceKg],
           SUM(CASE WHEN m.[FromStage] = N'Baking' AND m.[ToStage] = N'Activated' THEN m.[QuantityKg] ELSE 0 END) AS [IssuedToActivatedKg]
    FROM dcenter.DCenter_ConsumableMovements m
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[BakingRecordId] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@Ids))
    GROUP BY m.[BakingRecordId];
END
GO
-- Kg per consumable, month and transaction type (receive, issue, return, finish, adjust) for the dashboards.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Ledger_Monthly
    @From DATE,
    @Before DATE = NULL,
    @Category NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.[ItemId], i.[Category], i.[Diameter], i.[Specification],
           YEAR(m.[TxnDate]) AS [Year], MONTH(m.[TxnDate]) AS [Month], m.[TxnType], SUM(m.[QuantityKg]) AS [Kg]
    FROM dcenter.DCenter_ConsumableMovements m
    JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[TxnDate] >= @From AND (@Before IS NULL OR m.[TxnDate] < @Before)
      AND m.[TxnType] IN (N'Receive', N'Issue', N'Return', N'Finish', N'Adjust')
      AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY l.[ItemId], i.[Category], i.[Diameter], i.[Specification], YEAR(m.[TxnDate]), MONTH(m.[TxnDate]), m.[TxnType]
    OPTION (RECOMPILE);
END
GO
-- What a welder picked and returned per consumable within a date window, with the last issue from Activated storage.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Ledger_Welder
    @WelderId INT,
    @Since DATE,
    @Until DATE = NULL,
    @ItemId INT = NULL,
    @Category NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.[ItemId],
           SUM(CASE WHEN m.[TxnType] = N'Issue' THEN m.[QuantityKg] ELSE 0 END) AS [Picked],
           SUM(CASE WHEN m.[TxnType] = N'Return' THEN m.[QuantityKg] ELSE 0 END) AS [Returned],
           MAX(CASE WHEN m.[TxnType] = N'Issue' AND m.[FromStage] = N'Activated' THEN m.[Id] END) AS [LastIssueId]
    FROM dcenter.DCenter_ConsumableMovements m
    JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE m.[IsVoided] = 0 AND m.[TxnType] <> N'Void' AND m.[WelderId] = @WelderId AND m.[TxnDate] >= @Since
      AND (@Until IS NULL OR m.[TxnDate] <= @Until)
      AND (@ItemId IS NULL OR l.[ItemId] = @ItemId)
      AND (@Category IS NULL OR i.[Category] = @Category)
    GROUP BY l.[ItemId]
    OPTION (RECOMPILE);
END
GO

/* ===== Consumables: ledger lines ===== */
-- Ledger lines with item, lot, welder, compartment and baking details. @Terms: JSON array; every term must match. @Sort: 1 Id, 2 Id desc, 3 newest first, 4 type/spec/lot.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Movement_List
    @TxnNo NVARCHAR(20) = NULL,
    @LiveOnly BIT = 0,
    @WelderId INT = NULL,
    @CreatedFrom DATETIME2 = NULL,
    @TxnType NVARCHAR(20) = NULL,
    @ExcludeVoidEntries BIT = 0,
    @ReferenceNo NVARCHAR(60) = NULL,
    @From DATE = NULL,
    @To DATE = NULL,
    @Stage NVARCHAR(20) = NULL,
    @Category NVARCHAR(30) = NULL,
    @ItemId INT = NULL,
    @LotId INT = NULL,
    @CompartmentId INT = NULL,
    @BakingRecordId INT = NULL,
    @Terms NVARCHAR(MAX) = NULL,
    @Sort INT = 1,
    @Skip INT = 0,
    @Take INT = 2147483647,
    @Total INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @Total = COUNT(*)
    FROM dcenter.DCenter_ConsumableMovements m
    JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE (@TxnNo IS NULL OR m.[TxnNo] = @TxnNo)
      AND (@LiveOnly = 0 OR (m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'))
      AND (@WelderId IS NULL OR m.[WelderId] = @WelderId)
      AND (@CreatedFrom IS NULL OR m.[CreatedAt] >= @CreatedFrom)
      AND (@TxnType IS NULL OR m.[TxnType] = @TxnType)
      AND (@ExcludeVoidEntries = 0 OR m.[TxnType] <> N'Void')
      AND (@ReferenceNo IS NULL OR m.[ReferenceNo] = @ReferenceNo)
      AND (@From IS NULL OR m.[TxnDate] >= @From)
      AND (@To IS NULL OR m.[TxnDate] <= @To)
      AND (@Stage IS NULL OR m.[FromStage] = @Stage OR m.[ToStage] = @Stage)
      AND (@Category IS NULL OR i.[Category] = @Category)
      AND (@ItemId IS NULL OR l.[ItemId] = @ItemId)
      AND (@LotId IS NULL OR m.[LotId] = @LotId)
      AND (@CompartmentId IS NULL OR m.[FromCompartmentId] = @CompartmentId OR m.[ToCompartmentId] = @CompartmentId)
      AND (@BakingRecordId IS NULL OR m.[BakingRecordId] = @BakingRecordId)
      AND (SELECT COUNT(*) FROM OPENJSON(@Terms) t WHERE CHARINDEX(t.[value], m.[TxnNo]) > 0 OR CHARINDEX(t.[value], l.[Brand]) > 0 OR CHARINDEX(t.[value], l.[LotNumber]) > 0 OR CHARINDEX(t.[value], i.[Specification]) > 0 OR CHARINDEX(t.[value], i.[Diameter]) > 0 OR CHARINDEX(t.[value], m.[Requestor]) > 0 OR CHARINDEX(t.[value], m.[Remarks]) > 0 OR CHARINDEX(t.[value], m.[CreatedBy]) > 0) = (SELECT COUNT(*) FROM OPENJSON(@Terms))
    OPTION (RECOMPILE);

    SELECT m.[Id], m.[TxnNo], m.[TxnType], m.[TxnDate], m.[CreatedAt], m.[CreatedBy],
           l.[ItemId], i.[Category], i.[Specification], i.[Diameter], i.[Diameter] + N' ' + i.[Specification] AS [DiaSpec],
           m.[LotId], l.[Brand], l.[LotNumber], m.[QuantityKg], m.[FromStage], m.[ToStage],
           m.[Source], m.[Requestor], m.[WelderId], w.[WelderName], m.[Reason], m.[CountedQtyKg],
           m.[ReferenceNo], m.[Remarks], m.[IsVoided], m.[VoidsMovementId],
           m.[FromCompartmentId], fo.[Code] + N'-' + fc.[Label] AS [FromCompartment],
           m.[ToCompartmentId], tov.[Code] + N'-' + tc.[Label] AS [ToCompartment],
           m.[BakingRecordId], b.[BakingNo]
    FROM dcenter.DCenter_ConsumableMovements m
    JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId]
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    LEFT JOIN dcenter.DCenter_Welders w ON w.[Id] = m.[WelderId]
    LEFT JOIN dcenter.DCenter_OvenCompartments fc ON fc.[Id] = m.[FromCompartmentId]
    LEFT JOIN dcenter.DCenter_Ovens fo ON fo.[Id] = fc.[OvenId]
    LEFT JOIN dcenter.DCenter_OvenCompartments tc ON tc.[Id] = m.[ToCompartmentId]
    LEFT JOIN dcenter.DCenter_Ovens tov ON tov.[Id] = tc.[OvenId]
    LEFT JOIN dcenter.DCenter_BakingRecords b ON b.[Id] = m.[BakingRecordId]
    WHERE (@TxnNo IS NULL OR m.[TxnNo] = @TxnNo)
      AND (@LiveOnly = 0 OR (m.[IsVoided] = 0 AND m.[TxnType] <> N'Void'))
      AND (@WelderId IS NULL OR m.[WelderId] = @WelderId)
      AND (@CreatedFrom IS NULL OR m.[CreatedAt] >= @CreatedFrom)
      AND (@TxnType IS NULL OR m.[TxnType] = @TxnType)
      AND (@ExcludeVoidEntries = 0 OR m.[TxnType] <> N'Void')
      AND (@ReferenceNo IS NULL OR m.[ReferenceNo] = @ReferenceNo)
      AND (@From IS NULL OR m.[TxnDate] >= @From)
      AND (@To IS NULL OR m.[TxnDate] <= @To)
      AND (@Stage IS NULL OR m.[FromStage] = @Stage OR m.[ToStage] = @Stage)
      AND (@Category IS NULL OR i.[Category] = @Category)
      AND (@ItemId IS NULL OR l.[ItemId] = @ItemId)
      AND (@LotId IS NULL OR m.[LotId] = @LotId)
      AND (@CompartmentId IS NULL OR m.[FromCompartmentId] = @CompartmentId OR m.[ToCompartmentId] = @CompartmentId)
      AND (@BakingRecordId IS NULL OR m.[BakingRecordId] = @BakingRecordId)
      AND (SELECT COUNT(*) FROM OPENJSON(@Terms) t WHERE CHARINDEX(t.[value], m.[TxnNo]) > 0 OR CHARINDEX(t.[value], l.[Brand]) > 0 OR CHARINDEX(t.[value], l.[LotNumber]) > 0 OR CHARINDEX(t.[value], i.[Specification]) > 0 OR CHARINDEX(t.[value], i.[Diameter]) > 0 OR CHARINDEX(t.[value], m.[Requestor]) > 0 OR CHARINDEX(t.[value], m.[Remarks]) > 0 OR CHARINDEX(t.[value], m.[CreatedBy]) > 0) = (SELECT COUNT(*) FROM OPENJSON(@Terms))
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
-- Raw ledger lines of one transaction number, or by Id.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Movement_Get
    @TxnNo NVARCHAR(20) = NULL,
    @NotVoidedOnly BIT = 0,
    @Ids NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [TxnNo], [TxnType], [TxnDate], [LotId], [QuantityKg], [FromStage], [ToStage], [FromCompartmentId], [ToCompartmentId], [BakingRecordId], [Source], [Requestor], [WelderId], [Reason], [CountedQtyKg], [ReferenceNo], [Remarks], [IsVoided], [VoidsMovementId], [CreatedBy], [CreatedAt]
    FROM dcenter.DCenter_ConsumableMovements
    WHERE (@TxnNo IS NULL OR [TxnNo] = @TxnNo)
      AND (@NotVoidedOnly = 0 OR [IsVoided] = 0)
      AND (@Ids IS NULL OR [Id] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@Ids)))
    ORDER BY [Id]
    OPTION (RECOMPILE);
END
GO
-- @Rows: JSON array of ledger lines (with Seq) added in Seq order, so their Ids follow the order they were built in. @VoidIds: lines to mark voided.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Movement_Save
    @Rows NVARCHAR(MAX),
    @VoidIds NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    UPDATE dcenter.DCenter_ConsumableMovements
    SET [IsVoided] = 1
    WHERE [Id] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@VoidIds));

    INSERT INTO dcenter.DCenter_ConsumableMovements ([TxnNo], [TxnType], [TxnDate], [LotId], [QuantityKg], [FromStage], [ToStage], [FromCompartmentId], [ToCompartmentId], [BakingRecordId], [Source], [Requestor], [WelderId], [Reason], [CountedQtyKg], [ReferenceNo], [Remarks], [IsVoided], [VoidsMovementId], [CreatedBy], [CreatedAt])
    SELECT [TxnNo], [TxnType], [TxnDate], [LotId], [QuantityKg], [FromStage], [ToStage], [FromCompartmentId], [ToCompartmentId], [BakingRecordId], [Source], [Requestor], [WelderId], [Reason], [CountedQtyKg], [ReferenceNo], [Remarks], [IsVoided], [VoidsMovementId], [CreatedBy], [CreatedAt]
    FROM OPENJSON(@Rows) WITH ([Seq] INT, [TxnNo] NVARCHAR(20), [TxnType] NVARCHAR(20), [TxnDate] DATE, [LotId] INT, [QuantityKg] DECIMAL(10, 2), [FromStage] NVARCHAR(20), [ToStage] NVARCHAR(20), [FromCompartmentId] INT, [ToCompartmentId] INT, [BakingRecordId] INT, [Source] NVARCHAR(20), [Requestor] NVARCHAR(200), [WelderId] INT, [Reason] NVARCHAR(40), [CountedQtyKg] DECIMAL(10, 2), [ReferenceNo] NVARCHAR(60), [Remarks] NVARCHAR(500), [IsVoided] BIT, [VoidsMovementId] INT, [CreatedBy] NVARCHAR(100), [CreatedAt] DATETIME2)
    ORDER BY [Seq];

    COMMIT TRANSACTION;
END
GO

/* ===== Consumables: items and lots ===== */
-- Consumables by Id, type, search terms or specification + diameter. @WithHistory adds how many ledger lines and baking records each one has.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Item_List
    @Ids NVARCHAR(MAX) = NULL,
    @Category NVARCHAR(30) = NULL,
    @Terms NVARCHAR(MAX) = NULL,
    @ActiveOnly BIT = 0,
    @Specification NVARCHAR(100) = NULL,
    @Diameter NVARCHAR(30) = NULL,
    @ExcludeId INT = NULL,
    @WithHistory BIT = 0,
    @Take INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(@Take, 2147483647)) i.[Id], i.[Category], i.[Specification], i.[Diameter], i.[MinStockKg], i.[ActivatedMinKg], i.[FinishThresholdKg], i.[HoldingOvenType], i.[IsActive], i.[CreatedAt],
           ISNULL(h.[Movements], 0) AS [MovementCount], ISNULL(h.[Bakings], 0) AS [BakingCount]
    FROM dcenter.DCenter_ConsumableItems i
    OUTER APPLY
    (
        SELECT
            (SELECT COUNT(*) FROM dcenter.DCenter_ConsumableMovements m JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = m.[LotId] WHERE l.[ItemId] = i.[Id]) AS [Movements],
            (SELECT COUNT(*) FROM dcenter.DCenter_BakingRecords b JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId] WHERE l.[ItemId] = i.[Id]) AS [Bakings]
        WHERE @WithHistory = 1
    ) h
    WHERE (@Ids IS NULL OR i.[Id] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@Ids)))
      AND (@Category IS NULL OR i.[Category] = @Category)
      AND (@ActiveOnly = 0 OR i.[IsActive] = 1)
      AND (@Specification IS NULL OR i.[Specification] = @Specification)
      AND (@Diameter IS NULL OR i.[Diameter] = @Diameter)
      AND (@ExcludeId IS NULL OR i.[Id] <> @ExcludeId)
      AND (SELECT COUNT(*) FROM OPENJSON(@Terms) t WHERE CHARINDEX(t.[value], i.[Specification]) > 0 OR CHARINDEX(t.[value], i.[Diameter]) > 0 OR CHARINDEX(t.[value], i.[Category]) > 0) = (SELECT COUNT(*) FROM OPENJSON(@Terms))
    ORDER BY i.[Category], i.[Specification], i.[Diameter], i.[Id]
    OPTION (RECOMPILE);
END
GO
-- @Id NULL inserts a consumable; otherwise updates it. Returns the Id.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Item_Save
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
        INSERT INTO dcenter.DCenter_ConsumableItems
            ([Category], [Specification], [Diameter], [MinStockKg], [ActivatedMinKg], [FinishThresholdKg], [HoldingOvenType], [IsActive], [CreatedAt])
        VALUES (@Category, @Specification, @Diameter, @MinStockKg, @ActivatedMinKg, @FinishThresholdKg, @HoldingOvenType, @IsActive, @CreatedAt);
        SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Value];
        RETURN;
    END

    UPDATE dcenter.DCenter_ConsumableItems
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
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Item_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DELETE FROM dcenter.DCenter_ConsumableItemLots WHERE [ItemId] = @Id;
    DELETE FROM dcenter.DCenter_ConsumableItems WHERE [Id] = @Id;
    IF @@ROWCOUNT = 0
        THROW 50001, N'The consumable was changed or deleted by someone else.', 1;
    COMMIT TRANSACTION;
END
GO
-- Lots with their consumable, by Id, consumable, type, or brand + lot number.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Lot_List
    @Ids NVARCHAR(MAX) = NULL,
    @ItemIds NVARCHAR(MAX) = NULL,
    @Category NVARCHAR(30) = NULL,
    @Brand NVARCHAR(100) = NULL,
    @LotNumber NVARCHAR(60) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.[Id], l.[ItemId], l.[Brand], l.[LotNumber], l.[CreatedAt],
           i.[Category], i.[Specification], i.[Diameter], i.[HoldingOvenType], i.[MinStockKg], i.[IsActive]
    FROM dcenter.DCenter_ConsumableItemLots l
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE (@Ids IS NULL OR l.[Id] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@Ids)))
      AND (@ItemIds IS NULL OR l.[ItemId] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@ItemIds)))
      AND (@Category IS NULL OR i.[Category] = @Category)
      AND (@Brand IS NULL OR l.[Brand] = @Brand)
      AND (@LotNumber IS NULL OR l.[LotNumber] = @LotNumber)
    ORDER BY l.[Id]
    OPTION (RECOMPILE);
END
GO
-- Adds a lot and returns its Id.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Lot_Save
    @ItemId INT,
    @Brand NVARCHAR(100),
    @LotNumber NVARCHAR(60),
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dcenter.DCenter_ConsumableItemLots ([ItemId], [Brand], [LotNumber], [CreatedAt])
    VALUES (@ItemId, @Brand, @LotNumber, @CreatedAt);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Value];
END
GO

/* ===== Consumables: ovens, baking, holding, stock counts ===== */
-- Ovens and their compartments (FullLabel is oven code-label); @Ids limits it to those compartments.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Oven_Compartments
    @Ids NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.[Id] AS [OvenId], o.[Name], o.[Code], o.[OvenType],
           c.[Id] AS [CompartmentId], c.[Number], c.[Label], o.[Code] + N'-' + c.[Label] AS [FullLabel]
    FROM dcenter.DCenter_Ovens o
    LEFT JOIN dcenter.DCenter_OvenCompartments c ON c.[OvenId] = o.[Id]
    WHERE (@Ids IS NULL OR c.[Id] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@Ids)))
    ORDER BY o.[Id], c.[Number]
    OPTION (RECOMPILE);
END
GO
-- Baking records with their lot and consumable, newest first, with the total count. @Statuses and @Terms are JSON arrays.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Baking_List
    @Ids NVARCHAR(MAX) = NULL,
    @LotId INT = NULL,
    @From DATE = NULL,
    @To DATE = NULL,
    @Status NVARCHAR(20) = NULL,
    @Statuses NVARCHAR(MAX) = NULL,
    @ExcludeStatus NVARCHAR(20) = NULL,
    @BakeStoppedOnly BIT = 0,
    @Terms NVARCHAR(MAX) = NULL,
    @Skip INT = 0,
    @Take INT = 2147483647,
    @Total INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Matches TABLE ([Id] INT NOT NULL PRIMARY KEY);
    INSERT INTO @Matches ([Id])
    SELECT b.[Id]
    FROM dcenter.DCenter_BakingRecords b
    JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId]
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE (@Ids IS NULL OR b.[Id] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@Ids)))
      AND (@LotId IS NULL OR b.[LotId] = @LotId)
      AND (@From IS NULL OR b.[BakingDate] >= @From)
      AND (@To IS NULL OR b.[BakingDate] <= @To)
      AND (@Status IS NULL OR b.[Status] = @Status)
      AND (@Statuses IS NULL OR b.[Status] IN (SELECT [value] FROM OPENJSON(@Statuses)))
      AND (@ExcludeStatus IS NULL OR b.[Status] <> @ExcludeStatus)
      AND (@BakeStoppedOnly = 0 OR b.[BakeStop] IS NOT NULL)
      AND (SELECT COUNT(*) FROM OPENJSON(@Terms) t WHERE CHARINDEX(t.[value], b.[BakingNo]) > 0 OR CHARINDEX(t.[value], b.[PersonInCharge]) > 0 OR CHARINDEX(t.[value], l.[LotNumber]) > 0 OR CHARINDEX(t.[value], l.[Brand]) > 0 OR CHARINDEX(t.[value], i.[Specification]) > 0 OR CHARINDEX(t.[value], i.[Diameter]) > 0) = (SELECT COUNT(*) FROM OPENJSON(@Terms))
    OPTION (RECOMPILE);

    SELECT @Total = COUNT(*) FROM @Matches;

    SELECT b.[Id], b.[BakingNo], b.[LotId], b.[QuantityKg], b.[PersonInCharge], b.[BakingDate], b.[BakeStart], b.[BakeStop], b.[RebakeStart], b.[RebakeStop], b.[Status], b.[Remarks], b.[CreatedBy], b.[CreatedAt],
           l.[ItemId], i.[Category], i.[Diameter], i.[Specification], i.[HoldingOvenType], l.[Brand], l.[LotNumber]
    FROM dcenter.DCenter_BakingRecords b
    JOIN (SELECT [Id] FROM @Matches ORDER BY [Id] DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY) p ON p.[Id] = b.[Id]
    JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId]
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    ORDER BY b.[Id] DESC;
END
GO
-- @Rows: JSON array of baking records (with Seq). Rows with an Id update the editable columns and status; the others are inserted in Seq order and their Ids returned in that order.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Baking_Save
    @Rows NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Rs TABLE ([Seq] INT NOT NULL PRIMARY KEY, [Id] INT NULL, [BakingNo] NVARCHAR(20) NULL, [LotId] INT NULL, [QuantityKg] DECIMAL(10, 2) NULL,
                       [PersonInCharge] NVARCHAR(100) NOT NULL, [BakingDate] DATE NOT NULL, [BakeStart] DATETIME2 NULL, [BakeStop] DATETIME2 NULL,
                       [RebakeStart] DATETIME2 NULL, [RebakeStop] DATETIME2 NULL, [Status] NVARCHAR(20) NOT NULL, [Remarks] NVARCHAR(500) NULL,
                       [CreatedBy] NVARCHAR(100) NULL, [CreatedAt] DATETIME2 NULL);
    DECLARE @Ids TABLE ([Id] INT NOT NULL PRIMARY KEY);
    INSERT INTO @Rs
    SELECT * FROM OPENJSON(@Rows) WITH ([Seq] INT, [Id] INT, [BakingNo] NVARCHAR(20), [LotId] INT, [QuantityKg] DECIMAL(10, 2),
        [PersonInCharge] NVARCHAR(100), [BakingDate] DATE, [BakeStart] DATETIME2, [BakeStop] DATETIME2, [RebakeStart] DATETIME2,
        [RebakeStop] DATETIME2, [Status] NVARCHAR(20), [Remarks] NVARCHAR(500), [CreatedBy] NVARCHAR(100), [CreatedAt] DATETIME2);

    BEGIN TRANSACTION;

    UPDATE b
    SET [PersonInCharge] = r.[PersonInCharge], [BakingDate] = r.[BakingDate], [BakeStart] = r.[BakeStart], [BakeStop] = r.[BakeStop],
        [RebakeStart] = r.[RebakeStart], [RebakeStop] = r.[RebakeStop], [Status] = r.[Status], [Remarks] = r.[Remarks]
    FROM dcenter.DCenter_BakingRecords b
    JOIN @Rs r ON r.[Id] = b.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rs WHERE [Id] IS NOT NULL)
        THROW 50001, N'A baking record was changed or deleted by someone else.', 1;

    INSERT INTO dcenter.DCenter_BakingRecords
        ([BakingNo], [LotId], [QuantityKg], [PersonInCharge], [BakingDate], [BakeStart], [BakeStop], [RebakeStart], [RebakeStop], [Status], [Remarks], [CreatedBy], [CreatedAt])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [BakingNo], [LotId], [QuantityKg], [PersonInCharge], [BakingDate], [BakeStart], [BakeStop], [RebakeStart], [RebakeStop], [Status], [Remarks], [CreatedBy], [CreatedAt]
    FROM @Rs WHERE [Id] IS NULL ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT [Id] AS [Value] FROM @Ids ORDER BY [Id];
END
GO
-- Holding records (newest first) matching the filters, with the total count.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Holding_List
    @Id INT = NULL,
    @From DATE = NULL,
    @To DATE = NULL,
    @Terms NVARCHAR(MAX) = NULL,
    @Skip INT = 0,
    @Take INT = 2147483647,
    @Total INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Matches TABLE ([Id] INT NOT NULL PRIMARY KEY);
    INSERT INTO @Matches ([Id])
    SELECT h.[Id]
    FROM dcenter.DCenter_HoldingRecords h
    JOIN dcenter.DCenter_BakingRecords b ON b.[Id] = h.[BakingRecordId]
    JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId]
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    WHERE (@Id IS NULL OR h.[Id] = @Id)
      AND (@From IS NULL OR h.[HoldingDate] >= @From)
      AND (@To IS NULL OR h.[HoldingDate] <= @To)
      AND (SELECT COUNT(*) FROM OPENJSON(@Terms) t WHERE CHARINDEX(t.[value], h.[HoldingNo]) > 0 OR CHARINDEX(t.[value], b.[BakingNo]) > 0 OR CHARINDEX(t.[value], l.[LotNumber]) > 0 OR CHARINDEX(t.[value], i.[Specification]) > 0 OR CHARINDEX(t.[value], h.[WelderName]) > 0) = (SELECT COUNT(*) FROM OPENJSON(@Terms))
    OPTION (RECOMPILE);

    SELECT @Total = COUNT(*) FROM @Matches;

    SELECT h.[Id], h.[HoldingNo], h.[HoldingDate], h.[BakingRecordId], b.[BakingNo], l.[ItemId],
           i.[Diameter] + N' ' + i.[Specification] AS [DiaSpec],
           b.[LotId], l.[Brand], l.[LotNumber], h.[WelderId], h.[WelderName],
           h.[CompartmentId], o.[Code] + N'-' + c.[Label] AS [CompartmentLabel], o.[OvenType], c.[Number] AS [CompartmentNumber],
           h.[IsFinishedAfterBaking], h.[QuantityKg], h.[TxnNo], h.[IsVoided], h.[Remarks], h.[CreatedBy], h.[CreatedAt]
    FROM dcenter.DCenter_HoldingRecords h
    JOIN (SELECT [Id] FROM @Matches ORDER BY [Id] DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY) p ON p.[Id] = h.[Id]
    JOIN dcenter.DCenter_BakingRecords b ON b.[Id] = h.[BakingRecordId]
    JOIN dcenter.DCenter_ConsumableItemLots l ON l.[Id] = b.[LotId]
    JOIN dcenter.DCenter_ConsumableItems i ON i.[Id] = l.[ItemId]
    LEFT JOIN dcenter.DCenter_OvenCompartments c ON c.[Id] = h.[CompartmentId]
    LEFT JOIN dcenter.DCenter_Ovens o ON o.[Id] = c.[OvenId]
    ORDER BY h.[Id] DESC;
END
GO
-- Adds a holding record and returns its Id.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Holding_Save
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
    INSERT INTO dcenter.DCenter_HoldingRecords
        ([HoldingNo], [HoldingDate], [BakingRecordId], [WelderId], [WelderName], [CompartmentId], [IsFinishedAfterBaking],
         [QuantityKg], [TxnNo], [IsVoided], [Remarks], [CreatedBy], [CreatedAt])
    VALUES (@HoldingNo, @HoldingDate, @BakingRecordId, @WelderId, @WelderName, @CompartmentId, @IsFinishedAfterBaking,
            @QuantityKg, @TxnNo, 0, @Remarks, @CreatedBy, @CreatedAt);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Value];
END
GO
-- Voids the holding records created by one transaction.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Holding_Void
    @TxnNo NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dcenter.DCenter_HoldingRecords SET [IsVoided] = 1 WHERE [TxnNo] = @TxnNo AND [IsVoided] = 0;
END
GO
-- Moves open holding records of the given lots (JSON array) to another compartment.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Holding_Relocate
    @FromCompartmentId INT,
    @ToCompartmentId INT,
    @LotIds NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE h SET [CompartmentId] = @ToCompartmentId
    FROM dcenter.DCenter_HoldingRecords h
    JOIN dcenter.DCenter_BakingRecords b ON b.[Id] = h.[BakingRecordId]
    WHERE h.[IsVoided] = 0 AND h.[IsFinishedAfterBaking] = 0 AND h.[CompartmentId] = @FromCompartmentId
      AND b.[LotId] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@LotIds));
END
GO
-- Stock counts (newest first); IsVoided is set when the count's adjustment was voided.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_StockCount_List
    @Id INT = NULL,
    @ReferenceNo NVARCHAR(20) = NULL,
    @From DATE = NULL,
    @To DATE = NULL,
    @Scope NVARCHAR(20) = NULL,
    @Skip INT = 0,
    @Take INT = 2147483647,
    @Total INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @Total = COUNT(*)
    FROM dcenter.DCenter_StockCounts c
    WHERE (@Id IS NULL OR c.[Id] = @Id)
      AND (@ReferenceNo IS NULL OR c.[ReferenceNo] = @ReferenceNo)
      AND (@From IS NULL OR c.[CountDate] >= @From)
      AND (@To IS NULL OR c.[CountDate] <= @To)
      AND (@Scope IS NULL OR c.[Scope] = @Scope)
    OPTION (RECOMPILE);

    SELECT c.[Id], c.[ReferenceNo], c.[CountDate], c.[Scope], c.[Category], c.[LinesCounted], c.[LinesAdjusted],
           c.[GainKg], c.[LossKg], c.[TxnNo],
           CAST(CASE WHEN c.[TxnNo] IS NOT NULL AND EXISTS (
               SELECT 1 FROM dcenter.DCenter_ConsumableMovements m WHERE m.[TxnNo] = c.[TxnNo] AND m.[IsVoided] = 1)
               THEN 1 ELSE 0 END AS BIT) AS [IsVoided],
           c.[Remarks], c.[CreatedBy], c.[CreatedAt]
    FROM dcenter.DCenter_StockCounts c
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
-- Adds a stock count and returns its Id.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_StockCount_Save
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
    INSERT INTO dcenter.DCenter_StockCounts
        ([ReferenceNo], [CountDate], [Scope], [Category], [LinesCounted], [LinesAdjusted], [GainKg], [LossKg], [TxnNo], [Remarks], [CreatedBy], [CreatedAt])
    VALUES (@ReferenceNo, @CountDate, @Scope, @Category, @LinesCounted, @LinesAdjusted, @GainKg, @LossKg, @TxnNo, @Remarks, @CreatedBy, @CreatedAt);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Value];
END
GO

/* ===== Supervisor ===== */
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Supervisor_Credential
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [PasswordHash], [UpdatedBy], [UpdatedAt]
    FROM dcenter.DCenter_SupervisorCredentials
    WHERE [Id] = @Id;
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Supervisor_SaveCredential
    @Id INT,
    @PasswordHash NVARCHAR(400),
    @UpdatedBy NVARCHAR(200),
    @UpdatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    UPDATE dcenter.DCenter_SupervisorCredentials WITH (UPDLOCK, HOLDLOCK)
    SET [PasswordHash] = @PasswordHash, [UpdatedBy] = @UpdatedBy, [UpdatedAt] = @UpdatedAt
    WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        INSERT INTO dcenter.DCenter_SupervisorCredentials ([Id], [PasswordHash], [UpdatedBy], [UpdatedAt])
        VALUES (@Id, @PasswordHash, @UpdatedBy, @UpdatedAt);

    COMMIT TRANSACTION;
END
GO
-- Records a logged-out session token and drops the ones that have expired.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Supervisor_RevokeToken
    @Fingerprint VARCHAR(64),
    @ExpiresAt DATETIMEOFFSET,
    @Now DATETIMEOFFSET
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DELETE FROM dcenter.DCenter_SupervisorRevokedTokens WHERE [ExpiresAt] <= @Now;

    IF NOT EXISTS (SELECT 1 FROM dcenter.DCenter_SupervisorRevokedTokens WITH (UPDLOCK, HOLDLOCK) WHERE [Fingerprint] = @Fingerprint)
        INSERT INTO dcenter.DCenter_SupervisorRevokedTokens ([Fingerprint], [ExpiresAt]) VALUES (@Fingerprint, @ExpiresAt);

    COMMIT TRANSACTION;
END
GO
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Supervisor_ActiveRevocations
    @Now DATETIMEOFFSET
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Fingerprint], [ExpiresAt]
    FROM dcenter.DCenter_SupervisorRevokedTokens
    WHERE [ExpiresAt] > @Now;
END
GO

/* ===== Work orders (read through the views over OracleBetsyDB) ===== */
-- One row per work order, ordered by number. @Prefix NULL lists all; @Exact 1 matches the whole number.
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_WorkOrder_Search
    @Prefix VARCHAR(200),
    @Exact BIT,
    @Skip INT,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.[WO_NUMBER] AS [WorkOrderNumber],
           MAX(w.[ASSEMBLY_ITEM]) AS [AssemblyItem],
           MAX(w.[ITEM_DESC]) AS [AssemblyDesc],
           MAX(w.[START_QUANTITY]) AS [Qty]
    FROM dcenter.V_DCenter_WorkOrder w
    WHERE @Prefix IS NULL
       OR (@Exact = 1 AND w.[WO_NUMBER] = @Prefix)
       OR (@Exact = 0 AND w.[WO_NUMBER] LIKE REPLACE(REPLACE(REPLACE(REPLACE(@Prefix, '\', '\\'), '%', '\%'), '_', '\_'), '[', '\[') + '%' ESCAPE '\')
    GROUP BY w.[WO_NUMBER]
    ORDER BY w.[WO_NUMBER]
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
    OPTION (RECOMPILE);
END
GO
-- Direct children in the BOM of the given parent items (JSON array).
CREATE OR ALTER PROCEDURE dcenter.SP_DCenter_Bom_Children
    @Items NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Parents TABLE ([Item] VARCHAR(400) NOT NULL);
    INSERT INTO @Parents SELECT CAST([value] AS VARCHAR(400)) FROM OPENJSON(@Items);

    SELECT b.[ITEM] AS [Item], b.[COMPONENT] AS [Component], b.[COMPONENT_DESC] AS [ComponentDesc]
    FROM dcenter.V_DCenter_Bom b
    WHERE b.[ITEM] IN (SELECT [Item] FROM @Parents);
END
GO
