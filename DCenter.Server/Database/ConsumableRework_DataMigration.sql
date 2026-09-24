SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableItems) OR EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableMovements)
BEGIN
    RAISERROR(N'The new consumable tables already contain data. Migration aborted so nothing is duplicated.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

IF OBJECT_ID('tempdb..#map') IS NOT NULL DROP TABLE #map;
IF OBJECT_ID('tempdb..#lots') IS NOT NULL DROP TABLE #lots;
IF OBJECT_ID('tempdb..#lotmap') IS NOT NULL DROP TABLE #lotmap;
IF OBJECT_ID('tempdb..#numbered') IS NOT NULL DROP TABLE #numbered;
IF OBJECT_ID('tempdb..#txmap') IS NOT NULL DROP TABLE #txmap;
IF OBJECT_ID('tempdb..#mismatch') IS NOT NULL DROP TABLE #mismatch;

SELECT
    c.Id AS OldId,
    c.ConsumableType AS Category,
    UPPER(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(c.Specification)), N'   ', N' '), N'  ', N' '), N'  ', N' ')) AS Spec,
    d.DiaValue,
    CAST(FORMAT(d.DiaValue, N'0.##', N'en-US') AS nvarchar(30)) AS Dia,
    LTRIM(RTRIM(c.Manufacturer)) AS Brand,
    c.MinStockKg,
    c.IsActive
INTO #map
FROM dbo.DCenter_Consumables c
CROSS APPLY (
    SELECT TRY_CONVERT(decimal(9, 3),
        REPLACE(REPLACE(REPLACE(LOWER(LTRIM(RTRIM(c.Diameter))), N'mm', N''), N',', N'.'), N' ', N'')) AS DiaValue
) d;

IF EXISTS (SELECT 1 FROM #map WHERE DiaValue IS NULL OR DiaValue <= 0)
BEGIN
    SELECT c.Id, c.ConsumableType, c.Manufacturer, c.Specification, c.Diameter
    FROM dbo.DCenter_Consumables c
    JOIN #map m ON m.OldId = c.Id
    WHERE m.DiaValue IS NULL OR m.DiaValue <= 0;
    RAISERROR(N'Some diameters are not numeric (listed above). Correct them in DCenter_Consumables and run again.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

IF EXISTS (SELECT Spec, Dia FROM #map GROUP BY Spec, Dia HAVING COUNT(DISTINCT Category) > 1)
BEGIN
    SELECT Spec, Dia, STRING_AGG(Category, N' | ') AS Categories
    FROM (SELECT DISTINCT Spec, Dia, Category FROM #map) d
    GROUP BY Spec, Dia
    HAVING COUNT(*) > 1;
    RAISERROR(N'The specifications listed above exist under both consumable types. Fix the type in DCenter_Consumables and run again.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

INSERT INTO dbo.DCenter_ConsumableItems
    (Category, Specification, Diameter, MinStockKg, ActivatedMinKg, FinishThresholdKg, IsActive, CreatedAt)
SELECT MIN(Category), Spec, Dia, MAX(MinStockKg), 0, NULL, CAST(MAX(CAST(IsActive AS int)) AS bit), SYSDATETIME()
FROM #map
GROUP BY Spec, Dia;

SELECT l.Id AS OldLotId, i.Id AS ItemId, m.Brand, LTRIM(RTRIM(l.LotNumber)) AS LotNumber, l.CreatedAt
INTO #lots
FROM dbo.DCenter_ConsumableLots l
JOIN #map m ON m.OldId = l.ConsumableId
JOIN dbo.DCenter_ConsumableItems i ON i.Specification = m.Spec AND i.Diameter = m.Dia;

INSERT INTO dbo.DCenter_ConsumableItemLots (ItemId, Brand, LotNumber, CreatedAt)
SELECT ItemId, Brand, LotNumber, MIN(CreatedAt)
FROM #lots
GROUP BY ItemId, Brand, LotNumber;

SELECT o.OldLotId, n.Id AS NewLotId
INTO #lotmap
FROM #lots o
JOIN dbo.DCenter_ConsumableItemLots n
    ON n.ItemId = o.ItemId AND n.Brand = o.Brand AND n.LotNumber = o.LotNumber;

SELECT
    t.Id AS OldId,
    t.TxnType,
    t.TxnDate,
    lm.NewLotId,
    ABS(t.QuantityKg) AS QuantityKg,
    CASE t.Location WHEN N'Weldshop' THEN N'Weld Shop' ELSE N'Tool Crib' END AS Source,
    t.Requestor,
    t.ReferenceNo,
    t.Remarks,
    t.IsVoided,
    t.VoidsTxnId,
    t.CreatedAt,
    N'CT-' + RIGHT(CONVERT(nchar(4), YEAR(t.CreatedAt)), 2) + N'-'
        + RIGHT(N'000000' + CAST(ROW_NUMBER() OVER (ORDER BY t.Id) AS nvarchar(10)), 6) AS TxnNo,
    wm.WelderId
INTO #numbered
FROM dbo.DCenter_ConsumableTransactions t
JOIN #lotmap lm ON lm.OldLotId = t.LotId
OUTER APPLY (
    SELECT CASE WHEN COUNT(*) = 1 THEN MIN(w.Id) END AS WelderId
    FROM dbo.DCenter_Welders w
    WHERE w.WelderName = LTRIM(RTRIM(t.Requestor))
) wm;

IF (SELECT COUNT(*) FROM #numbered) <> (SELECT COUNT(*) FROM dbo.DCenter_ConsumableTransactions)
BEGIN
    RAISERROR(N'Some old transactions could not be mapped to a new lot. Migration aborted.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

CREATE TABLE #txmap (OldId int NOT NULL PRIMARY KEY, NewId int NOT NULL);

MERGE dbo.DCenter_ConsumableMovements AS tgt
USING (SELECT * FROM #numbered WHERE TxnType <> N'Void') AS src
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (TxnNo, TxnType, TxnDate, LotId, QuantityKg, FromStage, ToStage, Source, Requestor, WelderId,
            Reason, CountedQtyKg, ReferenceNo, Remarks, IsVoided, VoidsMovementId, CreatedBy, CreatedAt)
    VALUES (src.TxnNo, src.TxnType, src.TxnDate, src.NewLotId, src.QuantityKg,
            CASE WHEN src.TxnType = N'Issue' THEN N'Normal' END,
            CASE WHEN src.TxnType = N'Receive' THEN N'Normal' END,
            CASE WHEN src.TxnType = N'Receive' THEN src.Source END,
            src.Requestor,
            CASE WHEN src.TxnType = N'Issue' THEN src.WelderId END,
            NULL, NULL, ISNULL(src.ReferenceNo, N'MIGRATED'), src.Remarks, src.IsVoided, NULL, N'migration', src.CreatedAt)
OUTPUT src.OldId, inserted.Id INTO #txmap (OldId, NewId);

MERGE dbo.DCenter_ConsumableMovements AS tgt
USING (
    SELECT v.*, o.TxnType AS OriginalType, o.TxnNo AS OriginalTxnNo, map.NewId AS VoidsNewId
    FROM #numbered v
    JOIN #numbered o ON o.OldId = v.VoidsTxnId
    JOIN #txmap map ON map.OldId = v.VoidsTxnId
    WHERE v.TxnType = N'Void'
) AS src
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (TxnNo, TxnType, TxnDate, LotId, QuantityKg, FromStage, ToStage, Source, Requestor, WelderId,
            Reason, CountedQtyKg, ReferenceNo, Remarks, IsVoided, VoidsMovementId, CreatedBy, CreatedAt)
    VALUES (src.TxnNo, N'Void', src.TxnDate, src.NewLotId, src.QuantityKg,
            CASE WHEN src.OriginalType = N'Receive' THEN N'Normal' END,
            CASE WHEN src.OriginalType = N'Issue' THEN N'Normal' END,
            CASE WHEN src.OriginalType = N'Receive' THEN src.Source END,
            src.Requestor, NULL, NULL, NULL, src.OriginalTxnNo, src.Remarks, 0, src.VoidsNewId, N'migration', src.CreatedAt)
OUTPUT src.OldId, inserted.Id INTO #txmap (OldId, NewId);

IF (SELECT COUNT(*) FROM #txmap) <> (SELECT COUNT(*) FROM #numbered)
BEGIN
    RAISERROR(N'Not every old transaction was migrated (orphaned void entries?). Migration aborted.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

;WITH OldTotals AS (
    SELECT m.Spec, m.Dia, SUM(t.QuantityKg) AS Kg
    FROM dbo.DCenter_ConsumableTransactions t
    JOIN dbo.DCenter_ConsumableLots l ON l.Id = t.LotId
    JOIN #map m ON m.OldId = l.ConsumableId
    WHERE t.IsVoided = 0 AND t.TxnType <> N'Void'
    GROUP BY m.Spec, m.Dia
),
NewTotals AS (
    SELECT i.Specification AS Spec, i.Diameter AS Dia,
           SUM(CASE WHEN mv.ToStage = N'Normal' THEN mv.QuantityKg ELSE 0 END)
         - SUM(CASE WHEN mv.FromStage = N'Normal' THEN mv.QuantityKg ELSE 0 END) AS Kg
    FROM dbo.DCenter_ConsumableMovements mv
    JOIN dbo.DCenter_ConsumableItemLots l ON l.Id = mv.LotId
    JOIN dbo.DCenter_ConsumableItems i ON i.Id = l.ItemId
    WHERE mv.IsVoided = 0 AND mv.TxnType <> N'Void'
    GROUP BY i.Specification, i.Diameter
)
SELECT COALESCE(o.Spec, n.Spec) AS Spec, COALESCE(o.Dia, n.Dia) AS Dia, o.Kg AS OldKg, n.Kg AS NewKg
INTO #mismatch
FROM OldTotals o
FULL JOIN NewTotals n ON n.Spec = o.Spec AND n.Dia = o.Dia
WHERE ISNULL(o.Kg, 0) <> ISNULL(n.Kg, 0);

IF EXISTS (SELECT 1 FROM #mismatch)
BEGIN
    SELECT * FROM #mismatch;
    RAISERROR(N'Balances after migration do not match the old balances (listed above). Migration rolled back.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

UPDATE w
SET w.UsageScope = N'ReportAndStock'
FROM dbo.DCenter_Welders w
WHERE EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableMovements mv WHERE mv.WelderId = w.Id);

DECLARE @next bigint = (SELECT COUNT(*) FROM #numbered) + 1;
DECLARE @restart nvarchar(200) = N'ALTER SEQUENCE dbo.DCenter_ConsumableTxnSeq RESTART WITH ' + CAST(@next AS nvarchar(20)) + N';';
EXEC sys.sp_executesql @restart;

COMMIT TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM dbo.DCenter_ConsumableItems) AS Items,
    (SELECT COUNT(*) FROM dbo.DCenter_ConsumableItemLots) AS Lots,
    (SELECT COUNT(*) FROM dbo.DCenter_ConsumableMovements) AS Movements,
    (SELECT COUNT(*) FROM dbo.DCenter_Welders WHERE UsageScope = N'ReportAndStock') AS StockWelders;
