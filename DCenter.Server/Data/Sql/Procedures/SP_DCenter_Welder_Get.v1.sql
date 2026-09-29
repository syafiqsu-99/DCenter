CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Get
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WelderName], [WelderNo], [IsActive], [UsageScope]
    FROM dbo.V_DCenter_Welders
    WHERE [Id] = @Id;
END
