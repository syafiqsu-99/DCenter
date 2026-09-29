CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_FindByKey
    @Mrn NVARCHAR(100),
    @SpecNo NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Mrn], [SpecNo], [Form], [FullSpecification]
    FROM dbo.V_DCenter_MrnSpecs
    WHERE [Mrn] = @Mrn AND [SpecNo] = @SpecNo;
END
