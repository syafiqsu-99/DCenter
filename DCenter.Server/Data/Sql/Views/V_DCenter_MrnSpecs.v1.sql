CREATE OR ALTER VIEW dbo.V_DCenter_MrnSpecs
AS
SELECT
    [Id],
    [Mrn],
    [Form],
    [FullSpecification],
    [SpecNo]
FROM dbo.DCenter_MrnSpecs;
