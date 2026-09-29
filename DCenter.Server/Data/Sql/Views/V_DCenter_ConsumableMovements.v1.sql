CREATE OR ALTER VIEW dbo.V_DCenter_ConsumableMovements
AS
SELECT
    [Id],
    [TxnNo],
    [TxnType],
    [TxnDate],
    [LotId],
    [QuantityKg],
    [FromStage],
    [ToStage],
    [FromCompartmentId],
    [ToCompartmentId],
    [BakingRecordId],
    [Source],
    [Requestor],
    [WelderId],
    [Reason],
    [CountedQtyKg],
    [ReferenceNo],
    [Remarks],
    [IsVoided],
    [VoidsMovementId],
    [CreatedBy],
    [CreatedAt]
FROM dbo.DCenter_ConsumableMovements;
