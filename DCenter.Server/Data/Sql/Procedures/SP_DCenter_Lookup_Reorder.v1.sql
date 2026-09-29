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
