CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_List
    @Category NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Category], [Value], [SortOrder], [IsActive]
    FROM dbo.V_DCenter_Lookups
    WHERE @Category IS NULL OR [Category] = @Category
    ORDER BY [Category], [SortOrder], [Value];
END
