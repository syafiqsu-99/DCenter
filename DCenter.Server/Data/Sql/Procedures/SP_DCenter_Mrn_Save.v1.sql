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
    FROM dbo.V_DCenter_MrnSpecs v
    JOIN @Ids i ON i.[Id] = v.[Id]
    ORDER BY v.[Id];
END
