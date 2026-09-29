CREATE OR ALTER VIEW dbo.V_DCenter_SupervisorRevokedTokens
AS
SELECT
    [Fingerprint],
    [ExpiresAt]
FROM dbo.DCenter_SupervisorRevokedTokens;
