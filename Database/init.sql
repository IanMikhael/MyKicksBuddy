-- MyKicksBuddy local development schema
-- Derived from the current Dapper repositories because this repository has no
-- existing schema, migration, or seed script.
-- Safe to import into a fresh Laragon MySQL/MariaDB instance: it never drops
-- a database or table and it does not create demo users or orders.

CREATE DATABASE IF NOT EXISTS `mykicksbuddy`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE `mykicksbuddy`;

CREATE TABLE IF NOT EXISTS `users` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `role` VARCHAR(32) NOT NULL,
    `full_name` VARCHAR(255) NOT NULL,
    `email` VARCHAR(255) NULL,
    `phone` VARCHAR(64) NULL,
    `password_hash` VARCHAR(512) NOT NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `ux_users_email` (`email`),
    UNIQUE KEY `ux_users_phone` (`phone`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `customer_addresses` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `user_id` BIGINT NOT NULL,
    `label` VARCHAR(100) NOT NULL,
    `full_address` TEXT NOT NULL,
    `latitude` DOUBLE NOT NULL,
    `longitude` DOUBLE NOT NULL,
    `distance_km` DOUBLE NOT NULL,
    `is_within_radius` TINYINT(1) NOT NULL,
    `is_default` TINYINT(1) NOT NULL DEFAULT 0,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `ix_customer_addresses_user_id` (`user_id`),
    KEY `ix_customer_addresses_user_active` (`user_id`, `is_active`),
    CONSTRAINT `fk_customer_addresses_user`
        FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `services` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `name` VARCHAR(255) NOT NULL,
    `description` TEXT NULL,
    `price` DECIMAL(18,2) NOT NULL,
    `estimated_duration_days` INT NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `ux_services_name` (`name`),
    KEY `ix_services_active_name` (`is_active`, `name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `orders` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `order_code` VARCHAR(64) NOT NULL,
    `customer_id` BIGINT NOT NULL,
    `channel` VARCHAR(32) NOT NULL,
    `fulfillment_type` VARCHAR(64) NOT NULL,
    `address_id` BIGINT NULL,
    `distance_km` DECIMAL(10,2) NULL,
    `subtotal` DECIMAL(18,2) NOT NULL,
    `total` DECIMAL(18,2) NOT NULL,
    `status` VARCHAR(64) NOT NULL,
    `payment_status` VARCHAR(64) NOT NULL,
    `handled_by` BIGINT NULL,
    `notes` TEXT NULL,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `ux_orders_order_code` (`order_code`),
    KEY `ix_orders_customer_created_at` (`customer_id`, `created_at`),
    KEY `ix_orders_status_created_at` (`status`, `created_at`),
    KEY `ix_orders_payment_status_created_at` (`payment_status`, `created_at`),
    KEY `ix_orders_address_id` (`address_id`),
    KEY `ix_orders_handled_by` (`handled_by`),
    CONSTRAINT `fk_orders_customer`
        FOREIGN KEY (`customer_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_orders_address`
        FOREIGN KEY (`address_id`) REFERENCES `customer_addresses` (`id`),
    CONSTRAINT `fk_orders_handled_by`
        FOREIGN KEY (`handled_by`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `order_items` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `order_id` BIGINT NOT NULL,
    `service_id` BIGINT NOT NULL,
    `shoe_description` TEXT NULL,
    `qty` INT NOT NULL,
    `price` DECIMAL(18,2) NOT NULL,
    `subtotal` DECIMAL(18,2) NOT NULL,
    PRIMARY KEY (`id`),
    KEY `ix_order_items_order_id` (`order_id`),
    KEY `ix_order_items_service_id` (`service_id`),
    CONSTRAINT `fk_order_items_order`
        FOREIGN KEY (`order_id`) REFERENCES `orders` (`id`),
    CONSTRAINT `fk_order_items_service`
        FOREIGN KEY (`service_id`) REFERENCES `services` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `order_status_log` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `order_id` BIGINT NOT NULL,
    `status` VARCHAR(64) NOT NULL,
    `note` TEXT NULL,
    `changed_by` BIGINT NULL,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `ix_order_status_log_order_created_at` (`order_id`, `created_at`),
    KEY `ix_order_status_log_changed_by` (`changed_by`),
    CONSTRAINT `fk_order_status_log_order`
        FOREIGN KEY (`order_id`) REFERENCES `orders` (`id`),
    CONSTRAINT `fk_order_status_log_changed_by`
        FOREIGN KEY (`changed_by`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `payments` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `order_id` BIGINT NOT NULL,
    `provider` VARCHAR(64) NOT NULL,
    `provider_reference` VARCHAR(191) NOT NULL,
    `amount` DECIMAL(18,2) NOT NULL,
    `currency` CHAR(3) NOT NULL DEFAULT 'IDR',
    `status` VARCHAR(32) NOT NULL DEFAULT 'unpaid',
    `verified_at` DATETIME NULL,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `ux_payments_order` (`order_id`),
    UNIQUE KEY `ux_payments_provider_reference` (`provider`, `provider_reference`),
    CONSTRAINT `fk_payments_order` FOREIGN KEY (`order_id`) REFERENCES `orders` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
