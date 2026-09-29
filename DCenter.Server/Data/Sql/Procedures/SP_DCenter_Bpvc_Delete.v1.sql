CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Bpvc_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_BpvcIx WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The BPVC row was changed or deleted by someone else.', 1;
END
