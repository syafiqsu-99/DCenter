CREATE OR ALTER VIEW dbo.V_DCenter_Joints
AS
SELECT
    [Id],
    [ReportId],
    [JointNumber],
    [PartDescLeft],
    [PartNoLeft],
    [HeatNumberLeft],
    [PartDescRight],
    [PartNoRight],
    [HeatNumberRight],
    [WpsNo],
    [Rev],
    [WelderName],
    [WelderNo]
FROM dbo.DCenter_Joints;
