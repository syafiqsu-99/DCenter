CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Wps_FindByKey
    @WpsNo NVARCHAR(200),
    @PNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WpsNo], [PNo], [BaseMetal], [Process]
    FROM dbo.V_DCenter_WpsItems
    WHERE [WpsNo] = @WpsNo AND [PNo] = @PNo;
END
