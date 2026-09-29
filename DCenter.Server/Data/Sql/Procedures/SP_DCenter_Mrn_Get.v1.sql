CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_Get
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Mrn], [SpecNo], [Form], [FullSpecification]
    FROM dbo.V_DCenter_MrnSpecs
    WHERE [Id] = @Id;
END
