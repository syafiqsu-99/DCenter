CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Wps_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [WpsNo], [PNo], [BaseMetal], [Process]
    FROM dbo.V_DCenter_WpsItems
    ORDER BY [WpsNo], [PNo];
END
