CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_Get
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM dbo.V_DCenter_BpvcIx
    WHERE [Id] = @Id;
END
