-- Labor: AD-Gruppe des Buero-IT nachstellen (SQL-Rolle, weil die VMs SQL-Login nutzen)
-- Analog: MEISTER-FENSTER\ACL_Wochenplanungstool_RWX hat db_owner,
--         Mitglieder erben die Rechte nur wenn SQL sie wirklich als Mitglied sieht.

USE [Wochenplanung];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'lab_kollege')
    CREATE USER [lab_kollege] FOR LOGIN [lab_kollege];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ACL_Wochenplanungstool_RWX' AND type = 'R')
    CREATE ROLE [ACL_Wochenplanungstool_RWX];

-- Gruppe darf alles (wie db_owner an der Windows-Gruppe im Buero)
ALTER ROLE [db_owner] ADD MEMBER [ACL_Wochenplanungstool_RWX];

-- lab_kollege NICHT direkt db_owner, nur ueber die Gruppe
IF IS_ROLEMEMBER(N'db_owner', N'lab_kollege') = 1
    ALTER ROLE [db_owner] DROP MEMBER [lab_kollege];

ALTER ROLE [ACL_Wochenplanungstool_RWX] ADD MEMBER [lab_kollege];
GO
