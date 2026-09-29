-- Rows with an Id update that row; rows without one are inserted in Seq order. Returns the inserted rows.
CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_Save
    @Rows dbo.TT_DCenter_BpvcRows READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Ids TABLE ([Id] INT NOT NULL);

    BEGIN TRANSACTION;

    UPDATE t
    SET [SpecNo] = r.[SpecNo], [Designation] = r.[Designation], [UnsNo] = r.[UnsNo], [PNo] = r.[PNo], [MinTensile] = r.[MinTensile], [GroupNo] = r.[GroupNo], [IsoGroup] = r.[IsoGroup], [BrazingPNo] = r.[BrazingPNo], [NominalComposition] = r.[NominalComposition], [TypicalProductForm] = r.[TypicalProductForm], [NominalThicknessLimits] = r.[NominalThicknessLimits]
    FROM dbo.DCenter_BpvcIx t
    JOIN @Rows r ON r.[Id] = t.[Id];

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rows WHERE [Id] IS NOT NULL)
        THROW 50001, N'A BPVC row was changed or deleted by someone else.', 1;

    INSERT INTO dbo.DCenter_BpvcIx ([SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits])
    OUTPUT inserted.[Id] INTO @Ids
    SELECT [SpecNo], [Designation], [UnsNo], [PNo], [MinTensile], [GroupNo], [IsoGroup], [BrazingPNo], [NominalComposition], [TypicalProductForm], [NominalThicknessLimits]
    FROM @Rows
    WHERE [Id] IS NULL
    ORDER BY [Seq];

    COMMIT TRANSACTION;

    SELECT v.[Id], v.[SpecNo], v.[Designation], v.[UnsNo], v.[PNo], v.[MinTensile], v.[GroupNo], v.[IsoGroup], v.[BrazingPNo], v.[NominalComposition], v.[TypicalProductForm], v.[NominalThicknessLimits]
    FROM dbo.V_DCenter_BpvcIx v
    JOIN @Ids i ON i.[Id] = v.[Id]
    ORDER BY v.[Id];
END
