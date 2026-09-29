/*
    One-time cleanup for a DCenter database that ran the pre-release migrations DCenter18 / DCenter19
    (development databases only). Those migrations created one V_DCenter_<Table> view per DCenter
    table plus the Settings procedures; views over DCenter's own tables are no longer used, and the
    procedures are now maintained by DCenter_StoredProcedures.sql. Safe to re-run.

    Run it against the DCenter database, then run DCenter_StoredProcedures.sql.
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

DROP VIEW IF EXISTS dbo.V_DCenter_BakingRecords;
DROP VIEW IF EXISTS dbo.V_DCenter_BpvcIx;
DROP VIEW IF EXISTS dbo.V_DCenter_ConsumableItemLots;
DROP VIEW IF EXISTS dbo.V_DCenter_ConsumableItems;
DROP VIEW IF EXISTS dbo.V_DCenter_ConsumableMovements;
DROP VIEW IF EXISTS dbo.V_DCenter_HoldingRecords;
DROP VIEW IF EXISTS dbo.V_DCenter_JointMaterials;
DROP VIEW IF EXISTS dbo.V_DCenter_Joints;
DROP VIEW IF EXISTS dbo.V_DCenter_Lookups;
DROP VIEW IF EXISTS dbo.V_DCenter_MrnSpecs;
DROP VIEW IF EXISTS dbo.V_DCenter_OvenCompartments;
DROP VIEW IF EXISTS dbo.V_DCenter_Ovens;
DROP VIEW IF EXISTS dbo.V_DCenter_ProcessTypeLinks;
DROP VIEW IF EXISTS dbo.V_DCenter_ReportStatusEvents;
DROP VIEW IF EXISTS dbo.V_DCenter_Reports;
DROP VIEW IF EXISTS dbo.V_DCenter_StockCounts;
DROP VIEW IF EXISTS dbo.V_DCenter_SupervisorCredentials;
DROP VIEW IF EXISTS dbo.V_DCenter_SupervisorRevokedTokens;
DROP VIEW IF EXISTS dbo.V_DCenter_Welders;
DROP VIEW IF EXISTS dbo.V_DCenter_WpsItems;
GO

IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
    DELETE FROM dbo.__EFMigrationsHistory
    WHERE [MigrationId] IN (N'20260929065759_DCenter18', N'20260929070847_DCenter19');
GO
