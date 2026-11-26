-- Lab: zwei PCs + Server nachstellen (auf diesem Rechner = SQL-Server)
-- Ausfuehren in SSMS: verbunden als Windows-Admin an .\SQLEXPRESS
--
-- lab_sven      = Meister-PC (darf alles)
-- lab_kollege   = Kollegen-PC ohne Tabellenrechte (Employees-Fehler)

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'lab_sven')
    CREATE LOGIN [lab_sven] WITH PASSWORD = N'LabTest#2026', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'lab_kollege')
    CREATE LOGIN [lab_kollege] WITH PASSWORD = N'LabTest#2026', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
GO

USE [Wochenplanung];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'lab_sven')
    CREATE USER [lab_sven] FOR LOGIN [lab_sven];
ALTER ROLE [db_owner] ADD MEMBER [lab_sven];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'lab_kollege')
    CREATE USER [lab_kollege] FOR LOGIN [lab_kollege];
-- bewusst KEIN db_datareader / db_owner: nur Connect
GO

-- Optional: echten App-Benutzer nachziehen, falls Login schon existiert
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'wochenplanung')
   AND NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'wochenplanung')
    CREATE USER [wochenplanung] FOR LOGIN [wochenplanung];
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'wochenplanung')
    ALTER ROLE [db_owner] ADD MEMBER [wochenplanung];
GO

PRINT 'Lab-Logins: lab_sven (db_owner) und lab_kollege (nur Connect), Passwort LabTest#2026';
PRINT 'Gruppe nachstellen: installer/lab-gruppe.sql (Rolle ACL_Wochenplanungstool_RWX)';
GO
