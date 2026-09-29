CREATE OR ALTER VIEW dbo.V_DCenter_ConsumableItems
AS
SELECT
    [Id],
    [Category],
    [Specification],
    [Diameter],
    [MinStockKg],
    [ActivatedMinKg],
    [FinishThresholdKg],
    [HoldingOvenType],
    [IsActive],
    [CreatedAt]
FROM dbo.DCenter_ConsumableItems;
