CREATE OR ALTER PROCEDURE dbo.SP_DCenter_ProcessTypeLink_Insert
    @Process NVARCHAR(200),
    @Type NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    INSERT INTO dbo.DCenter_ProcessTypeLinks ([Process], [Type])
    OUTPUT inserted.[Id] INTO @Ids
    VALUES (@Process, @Type);

    SELECT v.[Id], v.[Process], v.[Type]
    FROM dbo.V_DCenter_ProcessTypeLinks v
    JOIN @Ids i ON i.[Id] = v.[Id];
END
