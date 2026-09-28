-- Additive provider-agnostic payment record; no provider or fake transaction is seeded.
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
