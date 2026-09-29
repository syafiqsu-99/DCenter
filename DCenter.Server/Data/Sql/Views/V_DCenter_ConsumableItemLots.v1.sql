CREATE OR ALTER VIEW dbo.V_DCenter_ConsumableItemLots
AS
SELECT
    [Id],
    [ItemId],
    [Brand],
    [LotNumber],
    [CreatedAt]
FROM dbo.DCenter_ConsumableItemLots;
