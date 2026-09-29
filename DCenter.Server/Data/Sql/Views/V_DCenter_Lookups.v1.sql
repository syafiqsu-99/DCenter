CREATE OR ALTER VIEW dbo.V_DCenter_Lookups
AS
SELECT
    [Id],
    [Category],
    [Value],
    [SortOrder],
    [IsActive]
FROM dbo.DCenter_Lookups;
