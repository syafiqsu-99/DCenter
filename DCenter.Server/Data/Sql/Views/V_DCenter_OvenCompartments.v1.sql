CREATE OR ALTER VIEW dbo.V_DCenter_OvenCompartments
AS
SELECT
    [Id],
    [OvenId],
    [Number],
    [Label]
FROM dbo.DCenter_OvenCompartments;
