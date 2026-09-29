CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Mrn], [SpecNo], [Form], [FullSpecification]
    FROM dbo.V_DCenter_MrnSpecs
    ORDER BY [Mrn], [SpecNo];
END
