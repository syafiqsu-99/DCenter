CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Wps_Get
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WpsNo], [PNo], [BaseMetal], [Process]
    FROM dbo.V_DCenter_WpsItems
    WHERE [Id] = @Id;
END
