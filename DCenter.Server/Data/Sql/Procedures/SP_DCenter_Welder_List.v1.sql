CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WelderName], [WelderNo], [IsActive], [UsageScope]
    FROM dbo.V_DCenter_Welders
    ORDER BY [WelderName];
END
