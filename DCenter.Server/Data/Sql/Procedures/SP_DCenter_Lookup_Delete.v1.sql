CREATE OR ALTER PROCEDURE dbo.SP_DCenter_Lookup_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DCenter_Lookups WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50001, N'The dropdown value was changed or deleted by someone else.', 1;
END
