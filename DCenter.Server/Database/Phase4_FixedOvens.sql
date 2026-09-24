SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Extra TABLE (CompartmentId int PRIMARY KEY);
INSERT INTO @Extra (CompartmentId)
SELECT c.Id
FROM dbo.DCenter_OvenCompartments c
WHERE c.OvenId NOT IN (1, 2, 3, 4) OR c.Number NOT BETWEEN 1 AND 9 OR c.Id <> (c.OvenId - 1) * 9 + c.Number;

IF EXISTS (
    SELECT 1 FROM dbo.DCenter_ConsumableMovements m
    WHERE m.FromCompartmentId IN (SELECT CompartmentId FROM @Extra) OR m.ToCompartmentId IN (SELECT CompartmentId FROM @Extra)
    UNION ALL
    SELECT 1 FROM dbo.DCenter_HoldingRecords h WHERE h.CompartmentId IN (SELECT CompartmentId FROM @Extra))
BEGIN
    SELECT o.Name AS Oven, c.Id AS CompartmentId, c.Number, c.Label
    FROM dbo.DCenter_OvenCompartments c
    JOIN dbo.DCenter_Ovens o ON o.Id = c.OvenId
    WHERE c.Id IN (SELECT CompartmentId FROM @Extra);
    ROLLBACK TRANSACTION;
    RAISERROR('Compartments outside the fixed layout have movement history. Void or move that stock to a fixed compartment, then rerun.', 16, 1);
    RETURN;
END;

DELETE FROM dbo.DCenter_OvenCompartments WHERE Id IN (SELECT CompartmentId FROM @Extra);
DELETE FROM dbo.DCenter_Ovens WHERE Id NOT IN (1, 2, 3, 4);

UPDATE dbo.DCenter_Ovens SET Name = CONCAT('~', Id), Code = CONCAT('~', Id) WHERE Id IN (1, 2, 3, 4);

MERGE dbo.DCenter_Ovens AS target
USING (VALUES
    (1, N'Alloy Steel Oven', N'AS', N'Alloy Steel'),
    (2, N'Mild Steel Oven', N'MS', N'Mild Steel'),
    (3, N'Ni Alloy Oven', N'NI', N'Ni Alloy'),
    (4, N'Stainless Steel Oven', N'SS', N'Stainless Steel')) AS source (Id, Name, Code, OvenType)
ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET Name = source.Name, Code = source.Code, OvenType = source.OvenType;

UPDATE dbo.DCenter_OvenCompartments SET Label = CONCAT(N'C', Number);

IF (SELECT COUNT(*) FROM dbo.DCenter_Ovens) <> 4 OR (SELECT COUNT(*) FROM dbo.DCenter_OvenCompartments) <> 36
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR('Expected 4 ovens and 36 compartments after normalising. Check the seed data before migrating.', 16, 1);
    RETURN;
END;

COMMIT TRANSACTION;
PRINT 'Ovens normalised to the fixed layout (4 ovens x 9 compartments).';
