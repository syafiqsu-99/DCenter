CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_IsDuplicate
    @Category NVARCHAR(50),
    @Value NVARCHAR(200),
    @ExcludeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM dbo.V_DCenter_Lookups
        WHERE [Category] = @Category AND [Value] = @Value AND [Id] <> @ExcludeId
    ) THEN 1 ELSE 0 END AS BIT) AS [Value];
END
