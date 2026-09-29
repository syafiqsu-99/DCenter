CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_SetScope
    @Ids dbo.TT_DCenter_IdList READONLY,
    @UsageScope NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE w
    SET [UsageScope] = @UsageScope
    FROM dbo.DCenter_Welders w
    JOIN @Ids i ON i.[Id] = w.[Id];

    SELECT @@ROWCOUNT AS [Value];
END
