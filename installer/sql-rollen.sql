-- Meisterfenster 2: SQL-Rollen statt überall db_owner.
-- Als sysadmin / db_owner einmal ausführen, dann Benutzer den Rollen zuweisen.
--
-- meisterfenster_planen  = Plan ändern (SELECT/INSERT/UPDATE/DELETE)
-- meisterfenster_lesen   = nur anschauen (SELECT)

USE [Wochenplanung];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'meisterfenster_planen' AND type = 'R')
    CREATE ROLE [meisterfenster_planen];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'meisterfenster_lesen' AND type = 'R')
    CREATE ROLE [meisterfenster_lesen];
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.Employees TO [meisterfenster_planen];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.Tasks TO [meisterfenster_planen];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.ScheduleTemplates TO [meisterfenster_planen];
GRANT SELECT ON OBJECT::dbo.__EFMigrationsHistory TO [meisterfenster_planen];

GRANT SELECT ON OBJECT::dbo.Employees TO [meisterfenster_lesen];
GRANT SELECT ON OBJECT::dbo.Tasks TO [meisterfenster_lesen];
GRANT SELECT ON OBJECT::dbo.ScheduleTemplates TO [meisterfenster_lesen];
GRANT SELECT ON OBJECT::dbo.__EFMigrationsHistory TO [meisterfenster_lesen];
GO

-- Standard-Schema, damit keine Tabellen unter dem Windows-Benutzer landen:
-- ALTER USER [MEISTER-FENSTER\sven.meister] WITH DEFAULT_SCHEMA = dbo;
-- ALTER ROLE [meisterfenster_planen] ADD MEMBER [MEISTER-FENSTER\sven.meister];
-- ALTER ROLE [meisterfenster_lesen] ADD MEMBER [MEISTER-FENSTER\kollege.muster];
GO
