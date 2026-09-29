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
    FROM dbo.V_DCenter_WpsItems v
    JOIN @Ids i ON i.[Id] = v.[Id]
    ORDER BY v.[Id];
END
