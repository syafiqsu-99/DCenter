CREATE OR ALTER VIEW dbo.V_DCenter_BakingRecords
AS
SELECT
    [Id],
    [BakingNo],
    [LotId],
    [QuantityKg],
    [PersonInCharge],
    [BakingDate],
    [BakeStart],
    [BakeStop],
    [RebakeStart],
    [RebakeStop],
    [Status],
    [Remarks],
    [CreatedBy],
    [CreatedAt]
FROM dbo.DCenter_BakingRecords;
