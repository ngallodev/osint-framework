-- OSINT Framework Database Initialization
CREATE DATABASE IF NOT EXISTS OsintFramework CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE OsintFramework;

-- Create user if not exists (this will be handled by MariaDB image environment variables)
-- GRANT ALL PRIVILEGES ON OsintFramework.* TO 'osintuser'@'%';

-- The actual tables will be created by Entity Framework migrations
-- This file can be used for any additional database setup
