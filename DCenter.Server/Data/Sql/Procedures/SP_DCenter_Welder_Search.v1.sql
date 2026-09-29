CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Search
    @Q NVARCHAR(4000),
    @StockOnly BIT,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Take) [Id], [WelderName], [WelderNo], [IsActive], [UsageScope]
    FROM dbo.V_DCenter_Welders
    WHERE [IsActive] = 1
      AND (@StockOnly = 0 OR [UsageScope] = N'ReportAndStock')
      AND (@Q = N'' OR CHARINDEX(@Q, [WelderName]) > 0 OR CHARINDEX(@Q, [WelderNo]) > 0)
    ORDER BY [WelderName];
END
