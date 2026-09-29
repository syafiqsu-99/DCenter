CREATE OR ALTER VIEW dbo.V_DCenter_StockCounts
AS
SELECT
    [Id],
    [ReferenceNo],
    [CountDate],
    [Scope],
    [Category],
    [LinesCounted],
    [LinesAdjusted],
    [GainKg],
    [LossKg],
    [TxnNo],
    [Remarks],
    [CreatedBy],
    [CreatedAt]
FROM dbo.DCenter_StockCounts;
