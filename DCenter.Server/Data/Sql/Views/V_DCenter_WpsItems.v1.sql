CREATE OR ALTER VIEW dbo.V_DCenter_WpsItems
AS
SELECT
    [Id],
    [WpsNo],
    [BaseMetal],
    [Process],
    [PNo]
FROM dbo.DCenter_WpsItems;
