/*
    One-time move of DCenter out of the shared database's dbo schema into its own schema [dcenter].
    Run it once on the DCenter database, with the site stopped, BEFORE starting the release that uses
    the [dcenter] schema. Safe to re-run.

    1. Creates schema [dcenter].
    2. Moves DCenter's rows of the EF migration history into dcenter.__EFMigrationsHistory. Rows of other
       applications in dbo.__EFMigrationsHistory are left as they are; that table is only dropped when
       nothing is left in it.
    3. Drops the DCenter procedures, table types, views and functions that earlier releases created in dbo
       (only names starting with SP_DCenter_, TT_DCenter_, V_DCenter_, vw_DCenter_ or fn_DCenter_).

    The DCenter tables and sequences themselves are moved (with their data) by the EF migration
    DCenterSchema when the new server starts, or by the DBA's migration script.

    Then run DCenter_SourceViews.sql and DCenter_StoredProcedures.sql, and start the site.
*/
SET NOCOUNT ON
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF SCHEMA_ID(N'dcenter') IS NULL EXEC (N'CREATE SCHEMA [dcenter] AUTHORIZATION [dbo]');
GO

IF OBJECT_ID(N'dcenter.__EFMigrationsHistory', N'U') IS NULL
    CREATE TABLE dcenter.__EFMigrationsHistory
    (
        [MigrationId] NVARCHAR(150) NOT NULL CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY,
        [ProductVersion] NVARCHAR(32) NOT NULL
    );
GO

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
BEGIN
    DECLARE @DCenter TABLE ([MigrationId] NVARCHAR(150) NOT NULL PRIMARY KEY);
    INSERT INTO @DCenter ([MigrationId]) VALUES
        (N'20260917084949_DCenter1'),
        (N'20260918064127_DCenter2'),
        (N'20260918071814_DCenter3'),
        (N'20260918082610_DCenter4'),
        (N'20260923021202_DCenter_5'),
        (N'20260923025000_DCenter_6'),
        (N'20260924010609_DCenter7'),
        (N'20260924011233_DCenter8'),
        (N'20260924020558_DCenter9'),
        (N'20260924022116_DCenter10'),
        (N'20260924100000_DCenter11'),
        (N'20260925010000_DCenter12'),
        (N'20260925020000_DCenter13'),
        (N'20260925030000_DCenter14'),
        (N'20260925040000_DCenter15'),
        (N'20260929010000_DCenter16'),
        (N'20260929023152_DCenter17');

    BEGIN TRANSACTION;

    INSERT INTO dcenter.__EFMigrationsHistory ([MigrationId], [ProductVersion])
    SELECT h.[MigrationId], h.[ProductVersion]
    FROM dbo.__EFMigrationsHistory h
    JOIN @DCenter d ON d.[MigrationId] = h.[MigrationId]
    WHERE NOT EXISTS (SELECT 1 FROM dcenter.__EFMigrationsHistory x WHERE x.[MigrationId] = h.[MigrationId]);

    DELETE h
    FROM dbo.__EFMigrationsHistory h
    WHERE h.[MigrationId] IN (SELECT [MigrationId] FROM @DCenter)
       OR h.[MigrationId] IN (N'20260929065759_DCenter18', N'20260929070847_DCenter19');

    COMMIT TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory)
        DROP TABLE dbo.__EFMigrationsHistory;
END
GO

DECLARE @Drop NVARCHAR(MAX) = N'';

SELECT @Drop += N'DROP ' + CASE o.[type] WHEN 'P' THEN N'PROCEDURE' WHEN 'V' THEN N'VIEW' ELSE N'FUNCTION' END
              + N' dbo.' + QUOTENAME(o.[name]) + N';' + NCHAR(10)
FROM sys.objects o
WHERE o.[schema_id] = SCHEMA_ID(N'dbo')
  AND (   (o.[type] = 'P' AND o.[name] LIKE N'SP[_]DCenter[_]%')
       OR (o.[type] = 'V' AND (o.[name] LIKE N'V[_]DCenter[_]%' OR o.[name] LIKE N'vw[_]DCenter[_]%'))
       OR (o.[type] IN ('FN', 'IF', 'TF') AND o.[name] LIKE N'fn[_]DCenter[_]%'));

SELECT @Drop += N'DROP TYPE dbo.' + QUOTENAME(t.[name]) + N';' + NCHAR(10)
FROM sys.types t
WHERE t.[is_user_defined] = 1 AND t.[schema_id] = SCHEMA_ID(N'dbo') AND t.[name] LIKE N'TT[_]DCenter[_]%';

IF @Drop <> N'' EXEC (@Drop);
GO
