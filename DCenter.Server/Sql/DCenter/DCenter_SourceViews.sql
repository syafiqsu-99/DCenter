/*
    DCenter work order views. Run this against the DCenter database (the DefaultConnection
    database), not OracleBetsyDB. Nothing is created or changed in OracleBetsyDB. Safe to re-run.

    Requirements
    - The DCenter database must be on the same SQL Server instance as OracleBetsyDB. On another
      machine (e.g. a localdb dev box), replace every "OracleBetsyDB.dbo." with a linked-server
      name such as "[MYNILASPMFGDB01].OracleBetsyDB.dbo.".
    - The login used by DefaultConnection needs SELECT on OracleBetsyDB.dbo.Work_Order_Detail
      and OracleBetsyDB.dbo.Bill_Of_Material_Others.

    If an earlier DCenter script created objects inside OracleBetsyDB, remove them there:
        -- USE [OracleBetsyDB];
        -- DROP VIEW IF EXISTS dbo.vw_DCenter_BomTree;
        -- DROP FUNCTION IF EXISTS dbo.fn_DCenter_WorkOrderBom;

    Re-run this script whenever it changes. The API walks a work order's BOM one level at a time
    through dbo.vw_DCenter_Bom (the children of a set of parent items per call).
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW dbo.vw_DCenter_WorkOrder
AS
SELECT w.WO_NUMBER, w.ASSEMBLY_ITEM, w.ITEM_DESC, w.START_QUANTITY
FROM OracleBetsyDB.dbo.Work_Order_Detail w;
GO

DROP FUNCTION IF EXISTS dbo.fn_DCenter_WorkOrderParts;
GO
DROP FUNCTION IF EXISTS dbo.fn_DCenter_BomTree;
GO
DROP VIEW IF EXISTS dbo.vw_DCenter_ItemMrn;
GO
DROP VIEW IF EXISTS dbo.vw_DCenter_BomTree;
GO

CREATE OR ALTER VIEW dbo.vw_DCenter_Bom
AS
SELECT b.ITEM, b.COMPONENT, MAX(b.COMPONENT_DESC) AS COMPONENT_DESC
FROM OracleBetsyDB.dbo.Bill_Of_Material_Others b
WHERE b.ITEM IS NOT NULL AND b.COMPONENT IS NOT NULL
GROUP BY b.ITEM, b.COMPONENT;
GO
