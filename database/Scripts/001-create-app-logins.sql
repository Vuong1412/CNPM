IF DB_ID(N'QOL') IS NULL
BEGIN
    CREATE DATABASE [QOL];
END;
GO

IF SUSER_ID(N'qol_app') IS NULL
BEGIN
    CREATE LOGIN [qol_app]
        WITH PASSWORD = N'$(APP_PASSWORD)', CHECK_POLICY = ON;
END;
GO

IF SUSER_ID(N'qol_migrator') IS NULL
BEGIN
    CREATE LOGIN [qol_migrator]
        WITH PASSWORD = N'$(MIGRATOR_PASSWORD)', CHECK_POLICY = ON;
END;
GO

USE [QOL];
GO

IF DATABASE_PRINCIPAL_ID(N'qol_app') IS NULL
BEGIN
    CREATE USER [qol_app] FOR LOGIN [qol_app];
END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS role_members
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = role_members.role_principal_id
    INNER JOIN sys.database_principals AS members
        ON members.principal_id = role_members.member_principal_id
    WHERE roles.name = N'db_datareader' AND members.name = N'qol_app'
)
    ALTER ROLE [db_datareader] ADD MEMBER [qol_app];
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS role_members
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = role_members.role_principal_id
    INNER JOIN sys.database_principals AS members
        ON members.principal_id = role_members.member_principal_id
    WHERE roles.name = N'db_datawriter' AND members.name = N'qol_app'
)
    ALTER ROLE [db_datawriter] ADD MEMBER [qol_app];
GO

IF DATABASE_PRINCIPAL_ID(N'qol_migrator') IS NULL
BEGIN
    CREATE USER [qol_migrator] FOR LOGIN [qol_migrator];
END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS role_members
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = role_members.role_principal_id
    INNER JOIN sys.database_principals AS members
        ON members.principal_id = role_members.member_principal_id
    WHERE roles.name = N'db_owner' AND members.name = N'qol_migrator'
)
    ALTER ROLE [db_owner] ADD MEMBER [qol_migrator];
GO
