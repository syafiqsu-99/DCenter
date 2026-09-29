CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_NumberHolder
    @WelderNo NVARCHAR(50),
    @ExcludeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) [WelderName] AS [Value]
    FROM dbo.V_DCenter_Welders
    WHERE [WelderNo] = @WelderNo AND [Id] <> @ExcludeId;
END
