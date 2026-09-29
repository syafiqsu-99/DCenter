CREATE OR ALTER PROCEDURE dbo.SP_DCenter_ProcessTypeLink_Exists
    @Process NVARCHAR(200),
    @Type NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM dbo.V_DCenter_ProcessTypeLinks WHERE [Process] = @Process AND [Type] = @Type
    ) THEN 1 ELSE 0 END AS BIT) AS [Value];
END
