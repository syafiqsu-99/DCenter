CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM dbo.V_DCenter_BpvcIx
    ORDER BY [SpecNo], [Designation], [UnsNo], [PNo];
END
