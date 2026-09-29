CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Welder_Update
    @Id INT,
    @WelderName NVARCHAR(200),
    @WelderNo NVARCHAR(50),
    @IsActive BIT,
    @UsageScope NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.DCenter_Welders
    SET [WelderName] = @WelderName, [WelderNo] = @WelderNo, [IsActive] = @IsActive, [UsageScope] = @UsageScope
    WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The welder was changed or deleted by someone else.', 1;
END
