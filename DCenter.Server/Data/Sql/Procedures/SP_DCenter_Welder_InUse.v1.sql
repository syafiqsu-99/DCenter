CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_InUse
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE
        WHEN EXISTS (SELECT 1 FROM dbo.V_DCenter_ConsumableMovements WHERE [WelderId] = @Id)
          OR EXISTS (SELECT 1 FROM dbo.V_DCenter_HoldingRecords WHERE [WelderId] = @Id)
        THEN 1 ELSE 0 END AS BIT) AS [Value];
END
