CREATE OR ALTER VIEW dbo.V_DCenter_Reports
AS
SELECT
    [Id],
    [WorkOrderNumber],
    [ReportRequired],
    [DateWelded],
    [PartNo],
    [Description],
    [MaterialSpec1],
    [MaterialSpec2],
    [MaterialSpec3],
    [Grade1],
    [Grade2],
    [Grade3],
    [PNumber1],
    [PNumber2],
    [PNumber3],
    [EngineerSupervisor],
    [QaInspector],
    [CreatedAt],
    [UpdatedAt],
    [CompletedAt],
    [RowVersion]
FROM dbo.DCenter_Reports;
