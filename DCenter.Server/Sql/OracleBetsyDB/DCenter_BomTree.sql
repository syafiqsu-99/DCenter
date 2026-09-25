/*
    DCenter multi-level bill of material view for OracleBetsyDB.
    Creates (or updates) one view: the full component tree under every item (ROOT_ITEM).
    DCenter filters it by the work order's assembly item with a parameterized query.
    Also drops fn_DCenter_WorkOrderBom from an earlier version of this script.
    Existing tables and their data are not modified. Safe to re-run.
*/
USE [OracleBetsyDB]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW dbo.vw_DCenter_BomTree
AS
WITH bom AS (
    SELECT DISTINCT b.ITEM, b.COMPONENT, b.COMPONENT_DESC
    FROM dbo.Bill_Of_Material_Others b
    WHERE b.ITEM IS NOT NULL AND b.COMPONENT IS NOT NULL
),
tree AS (
    SELECT
        b.ITEM AS ROOT_ITEM,
        1 AS BOM_LEVEL,
        b.ITEM AS PARENT_ITEM,
        b.COMPONENT,
        b.COMPONENT_DESC,
        CAST('/' + b.ITEM + '/' + b.COMPONENT + '/' AS varchar(4000)) AS BOM_PATH
    FROM bom b

    UNION ALL

    SELECT
        t.ROOT_ITEM,
        t.BOM_LEVEL + 1,
        c.ITEM,
        c.COMPONENT,
        c.COMPONENT_DESC,
        CAST(t.BOM_PATH + c.COMPONENT + '/' AS varchar(4000))
    FROM tree t
    JOIN bom c ON c.ITEM = t.COMPONENT
    WHERE t.BOM_LEVEL < 20
      AND CHARINDEX('/' + c.COMPONENT + '/', t.BOM_PATH) = 0
)
SELECT ROOT_ITEM, BOM_LEVEL, PARENT_ITEM, COMPONENT, COMPONENT_DESC, BOM_PATH
FROM tree;
GO

DROP FUNCTION IF EXISTS dbo.fn_DCenter_WorkOrderBom;
GO
