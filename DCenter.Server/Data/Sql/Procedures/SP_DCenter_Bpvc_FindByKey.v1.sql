CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_FindByKey
    @SpecNo NVARCHAR(100),
    @PNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM dbo.V_DCenter_BpvcIx
    WHERE [SpecNo] = @SpecNo AND [PNo] = @PNo;
END
