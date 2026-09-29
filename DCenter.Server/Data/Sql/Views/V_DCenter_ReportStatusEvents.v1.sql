CREATE OR ALTER VIEW dbo.V_DCenter_ReportStatusEvents
AS
SELECT
    [Id],
    [ReportId],
    [Action],
    [OccurredAt],
    [Details]
FROM dbo.DCenter_ReportStatusEvents;
