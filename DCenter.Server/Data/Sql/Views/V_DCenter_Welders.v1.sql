CREATE OR ALTER VIEW dbo.V_DCenter_Welders
AS
SELECT
    [Id],
    [WelderName],
    [WelderNo],
    [IsActive],
    [UsageScope]
FROM dbo.DCenter_Welders;
