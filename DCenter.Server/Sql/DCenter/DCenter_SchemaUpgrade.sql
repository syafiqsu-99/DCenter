/*
    One-time upgrade of an existing DCenter database to the [dcenter] schema layout without name prefixes.
    Run it once on the DCenter database, with the site stopped, BEFORE starting this release. Run it as
    a db_owner (or the DBA): moving a table to another schema needs CONTROL on that table, which the
    site's login usually does not have. Safe to re-run; on a new, empty database it does nothing.

        sqlcmd -S <server> -d DCenter -I -b -i DCenter_SchemaUpgrade.sql

    In one transaction (all or nothing):
    1. Moves the 20 DCenter_<Table> tables (from dbo or wherever they are) into [dcenter] and renames them
       to <Table>. Their keys, constraints and indexes lose the DCenter_ part of their names. Data is kept.
       SQL Server prints a "Caution: Changing any part of an object name..." line for each rename; that is expected.
    2. Replaces the four number sequences with one, dcenter.DocumentNoSeq, starting above every number
       already issued, so no document number can repeat.
    3. Leaves only the Baseline row as DCenter's EF migration history (dcenter.__EFMigrationsHistory), so
       the site does not migrate anything on start. Other applications' rows in dbo.__EFMigrationsHistory
       are left as they are; that table is only dropped when nothing is left in it.
    4. Drops the DCenter procedures, table types, views and functions that earlier releases created in dbo
       or dcenter (only names starting with SP_DCenter_, TT_DCenter_, V_DCenter_, vw_DCenter_ or fn_DCenter_).
       Nothing that belongs to other teams is touched.

    Then run DCenter_SourceViews.sql and DCenter_StoredProcedures.sql, and start the site.
*/
SET NOCOUNT ON
SET XACT_ABORT ON
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

DECLARE @Tables TABLE ([Name] SYSNAME NOT NULL PRIMARY KEY);
INSERT INTO @Tables ([Name]) VALUES
    (N'BakingRecords'), (N'BpvcIx'), (N'ConsumableItemLots'), (N'ConsumableItems'), (N'ConsumableMovements'),
    (N'HoldingRecords'), (N'JointMaterials'), (N'Joints'), (N'Lookups'), (N'MrnSpecs'), (N'OvenCompartments'),
    (N'Ovens'), (N'ProcessTypeLinks'), (N'ReportStatusEvents'), (N'Reports'), (N'StockCounts'),
    (N'SupervisorCredentials'), (N'SupervisorRevokedTokens'), (N'Welders'), (N'WpsItems');

DECLARE @OldMigrations TABLE ([MigrationId] NVARCHAR(150) NOT NULL PRIMARY KEY);
INSERT INTO @OldMigrations ([MigrationId]) VALUES
    (N'20260917084949_DCenter1'), (N'20260918064127_DCenter2'), (N'20260918071814_DCenter3'),
    (N'20260918082610_DCenter4'), (N'20260923021202_DCenter_5'), (N'20260923025000_DCenter_6'),
    (N'20260924010609_DCenter7'), (N'20260924011233_DCenter8'), (N'20260924020558_DCenter9'),
    (N'20260924022116_DCenter10'), (N'20260924100000_DCenter11'), (N'20260925010000_DCenter12'),
    (N'20260925020000_DCenter13'), (N'20260925030000_DCenter14'), (N'20260925040000_DCenter15'),
    (N'20260929010000_DCenter16'), (N'20260929023152_DCenter17'), (N'20260929065759_DCenter18'),
    (N'20260929070847_DCenter19'), (N'20260929084841_DCenterSchema');

DECLARE @Baseline NVARCHAR(150) = N'20260930013359_Baseline';
DECLARE @OldTables INT = (SELECT COUNT(*) FROM sys.tables t JOIN @Tables n ON t.[name] = N'DCenter_' + n.[Name]);
DECLARE @NewTables INT = (SELECT COUNT(*) FROM sys.tables t JOIN @Tables n ON t.[name] = n.[Name] WHERE t.[schema_id] = SCHEMA_ID(N'dcenter'));
DECLARE @Sql NVARCHAR(MAX), @Name SYSNAME, @From SYSNAME, @Message NVARCHAR(2048);

IF @OldTables > 0 AND IS_ROLEMEMBER(N'db_owner') <> 1
    THROW 50010, N'DCenter_SchemaUpgrade.sql must be run by a db_owner of the DCenter database (moving tables needs CONTROL on them). Ask the DBA to run it.', 1;

DECLARE @AtPreviousRelease BIT = 0;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE [MigrationId] = N'20260929023152_DCenter17') SET @AtPreviousRelease = 1;
IF OBJECT_ID(N'dcenter.__EFMigrationsHistory', N'U') IS NOT NULL
    IF EXISTS (SELECT 1 FROM dcenter.__EFMigrationsHistory WHERE [MigrationId] = N'20260929023152_DCenter17') SET @AtPreviousRelease = 1;

IF @OldTables > 0 AND @AtPreviousRelease = 0
    THROW 50011, N'This database is older than the previous DCenter release. Deploy and start the previous release once (so its migrations run), then run this script again.', 1;

IF @OldTables > 0 AND @OldTables < (SELECT COUNT(*) FROM @Tables) AND @NewTables = 0
BEGIN
    SET @Message = N'Only ' + CAST(@OldTables AS NVARCHAR(10)) + N' of the ' + CAST((SELECT COUNT(*) FROM @Tables) AS NVARCHAR(10)) +
                   N' DCenter_ tables are visible to this login. Run the script as a db_owner.';
    THROW 50012, @Message, 1;
END

IF EXISTS (SELECT 1 FROM @Tables n
           WHERE EXISTS (SELECT 1 FROM sys.tables t WHERE t.[name] = N'DCenter_' + n.[Name])
             AND OBJECT_ID(N'dcenter.' + QUOTENAME(n.[Name]), N'U') IS NOT NULL)
    THROW 50013, N'Some tables exist both with and without the DCenter_ prefix. Ask the developer before running this script.', 1;

BEGIN TRANSACTION;

IF SCHEMA_ID(N'dcenter') IS NULL EXEC (N'CREATE SCHEMA [dcenter] AUTHORIZATION [dbo]');

DECLARE tables CURSOR LOCAL FAST_FORWARD FOR
    SELECT n.[Name], SCHEMA_NAME(t.[schema_id])
    FROM @Tables n
    JOIN sys.tables t ON t.[name] = N'DCenter_' + n.[Name];
OPEN tables;
FETCH NEXT FROM tables INTO @Name, @From;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF @From <> N'dcenter'
    BEGIN
        SET @Sql = N'ALTER SCHEMA [dcenter] TRANSFER ' + QUOTENAME(@From) + N'.' + QUOTENAME(N'DCenter_' + @Name) + N';';
        EXEC (@Sql);
    END
    SET @Sql = N'dcenter.' + QUOTENAME(N'DCenter_' + @Name);
    EXEC sp_rename @Sql, @Name, N'OBJECT';
    FETCH NEXT FROM tables INTO @Name, @From;
END
CLOSE tables;
DEALLOCATE tables;

DECLARE @Renames TABLE ([Old] NVARCHAR(600) NOT NULL, [New] SYSNAME NOT NULL, [Kind] VARCHAR(10) NOT NULL);

INSERT INTO @Renames ([Old], [New], [Kind])
SELECT N'dcenter.' + QUOTENAME(o.[name]), REPLACE(o.[name], N'DCenter_', N''), 'OBJECT'
FROM sys.objects o
JOIN @Tables n ON OBJECT_NAME(o.[parent_object_id]) = n.[Name]
WHERE o.[schema_id] = SCHEMA_ID(N'dcenter') AND o.[type] IN ('PK', 'F', 'C', 'UQ') AND o.[name] LIKE N'%DCenter[_]%';

INSERT INTO @Renames ([Old], [New], [Kind])
SELECT N'dcenter.' + QUOTENAME(n.[Name]) + N'.' + QUOTENAME(i.[name]), REPLACE(i.[name], N'DCenter_', N''), 'INDEX'
FROM sys.indexes i
JOIN @Tables n ON i.[object_id] = OBJECT_ID(N'dcenter.' + QUOTENAME(n.[Name]))
WHERE i.[is_primary_key] = 0 AND i.[is_unique_constraint] = 0 AND i.[name] LIKE N'%DCenter[_]%';

DECLARE @Old NVARCHAR(600), @New SYSNAME, @Kind VARCHAR(10);
DECLARE renames CURSOR LOCAL FAST_FORWARD FOR SELECT [Old], [New], [Kind] FROM @Renames;
OPEN renames;
FETCH NEXT FROM renames INTO @Old, @New, @Kind;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sp_rename @Old, @New, @Kind;
    FETCH NEXT FROM renames INTO @Old, @New, @Kind;
END
CLOSE renames;
DEALLOCATE renames;

SET @Sql = N'';
SELECT @Sql += N'ALTER TABLE dcenter.' + QUOTENAME(OBJECT_NAME(d.[parent_object_id])) + N' DROP CONSTRAINT ' + QUOTENAME(d.[name]) + N';' + NCHAR(10)
FROM sys.default_constraints d
WHERE d.[parent_object_id] IN (OBJECT_ID(N'dcenter.MrnSpecs'), OBJECT_ID(N'dcenter.BpvcIx'))
  AND COL_NAME(d.[parent_object_id], d.[parent_column_id]) = N'SpecNo';
IF @Sql <> N'' EXEC (@Sql);

IF @OldTables > 0 AND OBJECT_ID(N'dcenter.DocumentNoSeq', N'SO') IS NULL
BEGIN
    DECLARE @Start BIGINT = 1 + ISNULL((
        SELECT MAX(CAST(ISNULL(s.[last_used_value], 0) AS BIGINT))
        FROM sys.sequences s
        WHERE s.[name] IN (N'DCenter_ConsumableTxnSeq', N'DCenter_BakingNoSeq', N'DCenter_HoldingNoSeq', N'DCenter_StockCountSeq')), 0);
    SET @Sql = N'CREATE SEQUENCE [dcenter].[DocumentNoSeq] START WITH ' + CAST(@Start AS NVARCHAR(20)) +
               N' INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;';
    EXEC (@Sql);
END

SET @Sql = N'';
SELECT @Sql += N'DROP SEQUENCE ' + QUOTENAME(SCHEMA_NAME(s.[schema_id])) + N'.' + QUOTENAME(s.[name]) + N';' + NCHAR(10)
FROM sys.sequences s
WHERE s.[name] IN (N'DCenter_ConsumableTxnSeq', N'DCenter_BakingNoSeq', N'DCenter_HoldingNoSeq', N'DCenter_StockCountSeq')
  AND OBJECT_ID(N'dcenter.DocumentNoSeq', N'SO') IS NOT NULL;
IF @Sql <> N'' EXEC (@Sql);

IF @OldTables > 0 OR @NewTables > 0
BEGIN
    IF OBJECT_ID(N'dcenter.__EFMigrationsHistory', N'U') IS NULL
        CREATE TABLE dcenter.__EFMigrationsHistory
        (
            [MigrationId] NVARCHAR(150) NOT NULL CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY,
            [ProductVersion] NVARCHAR(32) NOT NULL
        );

    DELETE FROM dcenter.__EFMigrationsHistory WHERE [MigrationId] IN (SELECT [MigrationId] FROM @OldMigrations);

    IF NOT EXISTS (SELECT 1 FROM dcenter.__EFMigrationsHistory WHERE [MigrationId] = @Baseline)
        INSERT INTO dcenter.__EFMigrationsHistory ([MigrationId], [ProductVersion]) VALUES (@Baseline, N'10.0.12');
END

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
BEGIN
    DELETE FROM dbo.__EFMigrationsHistory WHERE [MigrationId] IN (SELECT [MigrationId] FROM @OldMigrations);
    IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory) DROP TABLE dbo.__EFMigrationsHistory;
END

SET @Sql = N'';
SELECT @Sql += N'DROP ' + CASE o.[type] WHEN 'P' THEN N'PROCEDURE' WHEN 'V' THEN N'VIEW' ELSE N'FUNCTION' END
             + N' ' + QUOTENAME(SCHEMA_NAME(o.[schema_id])) + N'.' + QUOTENAME(o.[name]) + N';' + NCHAR(10)
FROM sys.objects o
WHERE o.[schema_id] IN (SCHEMA_ID(N'dbo'), SCHEMA_ID(N'dcenter'))
  AND (   (o.[type] = 'P' AND o.[name] LIKE N'SP[_]DCenter[_]%')
       OR (o.[type] = 'V' AND (o.[name] LIKE N'V[_]DCenter[_]%' OR o.[name] LIKE N'vw[_]DCenter[_]%'))
       OR (o.[type] IN ('FN', 'IF', 'TF') AND o.[name] LIKE N'fn[_]DCenter[_]%'));

SELECT @Sql += N'DROP TYPE ' + QUOTENAME(SCHEMA_NAME(t.[schema_id])) + N'.' + QUOTENAME(t.[name]) + N';' + NCHAR(10)
FROM sys.types t
WHERE t.[is_user_defined] = 1 AND t.[schema_id] IN (SCHEMA_ID(N'dbo'), SCHEMA_ID(N'dcenter')) AND t.[name] LIKE N'TT[_]DCenter[_]%';

IF @Sql <> N'' EXEC (@Sql);

COMMIT TRANSACTION;

IF @OldTables > 0 PRINT N'DCenter tables moved to [dcenter] and renamed. Now run DCenter_SourceViews.sql and DCenter_StoredProcedures.sql.';
ELSE IF @NewTables > 0 PRINT N'Already upgraded; nothing to move.';
ELSE PRINT N'No DCenter tables yet; the site creates them on first start.';
GO
