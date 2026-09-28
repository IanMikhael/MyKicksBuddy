-- Phase 1-2 additive migration for an existing MyKicksBuddy database.
-- Review against the target MySQL/MariaDB version, back up the database, then run once.
-- This migration does not seed users, services, or orders.

USE `mykicksbuddy`;

ALTER TABLE `customer_addresses`
    ADD COLUMN `is_active` TINYINT(1) NOT NULL DEFAULT 1 AFTER `is_default`,
    ADD COLUMN `updated_at` DATETIME NOT NULL
        DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER `created_at`;

ALTER TABLE `services`
    ADD COLUMN `description` TEXT NULL AFTER `name`,
    ADD COLUMN `estimated_duration_days` INT NULL AFTER `price`,
    ADD COLUMN `is_active` TINYINT(1) NOT NULL DEFAULT 1 AFTER `estimated_duration_days`,
    ADD COLUMN `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER `is_active`,
    ADD COLUMN `updated_at` DATETIME NOT NULL
        DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER `created_at`;
