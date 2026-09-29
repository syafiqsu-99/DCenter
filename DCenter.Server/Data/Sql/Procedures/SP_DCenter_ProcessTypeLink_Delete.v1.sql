-- Returns the number of rows deleted (0 when the link no longer exists).
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_ProcessTypeLink_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_ProcessTypeLinks WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [Value];
END
