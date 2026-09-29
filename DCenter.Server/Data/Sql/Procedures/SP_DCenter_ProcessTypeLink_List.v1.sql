CREATE OR ALTER PROCEDURE dbo.SP_DCenter_ProcessTypeLink_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Process], [Type]
    FROM dbo.V_DCenter_ProcessTypeLinks
    ORDER BY [Process], [Type];
END
