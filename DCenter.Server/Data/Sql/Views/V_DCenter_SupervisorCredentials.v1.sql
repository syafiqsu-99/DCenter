CREATE OR ALTER VIEW dbo.V_DCenter_SupervisorCredentials
AS
SELECT
    [Id],
    [PasswordHash],
    [UpdatedBy],
    [UpdatedAt]
FROM dbo.DCenter_SupervisorCredentials;
