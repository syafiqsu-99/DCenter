CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Insert
    @WelderName NVARCHAR(200),
    @WelderNo NVARCHAR(50),
    @IsActive BIT,
    @UsageScope NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    INSERT INTO dbo.DCenter_Welders ([WelderName], [WelderNo], [IsActive], [UsageScope])
    OUTPUT inserted.[Id] INTO @Ids
    VALUES (@WelderName, @WelderNo, @IsActive, @UsageScope);

    SELECT v.[Id], v.[WelderName], v.[WelderNo], v.[IsActive], v.[UsageScope]
    FROM dbo.V_DCenter_Welders v
    JOIN @Ids i ON i.[Id] = v.[Id];
END
