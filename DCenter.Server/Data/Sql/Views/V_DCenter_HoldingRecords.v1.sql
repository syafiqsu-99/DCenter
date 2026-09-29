CREATE OR ALTER VIEW dbo.V_DCenter_HoldingRecords
AS
SELECT
    [Id],
    [HoldingNo],
    [HoldingDate],
    [BakingRecordId],
    [WelderId],
    [WelderName],
    [CompartmentId],
    [IsFinishedAfterBaking],
    [QuantityKg],
    [TxnNo],
    [IsVoided],
    [Remarks],
    [CreatedBy],
    [CreatedAt]
FROM dbo.DCenter_HoldingRecords;
