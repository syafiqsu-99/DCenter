CREATE OR ALTER VIEW dbo.V_DCenter_JointMaterials
AS
SELECT
    [Id],
    [JointId],
    [ColumnNumber],
    [Process],
    [Size],
    [Type],
    [Manuf],
    [HeatLot]
FROM dbo.DCenter_JointMaterials;
