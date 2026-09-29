CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Mrn_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_MrnSpecs WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The MRN row was changed or deleted by someone else.', 1;
END
