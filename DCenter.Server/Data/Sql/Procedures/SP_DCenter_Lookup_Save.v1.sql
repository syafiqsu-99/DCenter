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
    FROM dbo.V_DCenter_Lookups v
    JOIN @Ids i ON i.[Id] = v.[Id]
    ORDER BY v.[Id];
END
