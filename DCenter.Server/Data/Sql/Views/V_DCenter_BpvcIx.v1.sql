CREATE OR ALTER VIEW dbo.V_DCenter_BpvcIx
AS
SELECT
    [Id],
    [Designation],
    [UnsNo],
    [MinTensile],
    [PNo],
    [GroupNo],
    [IsoGroup],
    [BrazingPNo],
    [NominalComposition],
    [TypicalProductForm],
    [NominalThicknessLimits],
    [SpecNo]
FROM dbo.DCenter_BpvcIx;
