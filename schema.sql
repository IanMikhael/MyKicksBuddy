-- ============================================================
-- SCHEMA DATABASE - MyKicksBuddy Shoes & Care
-- Engine: MySQL 8, InnoDB
-- Jalankan file ini untuk setup database dari nol
-- ============================================================

CREATE DATABASE IF NOT EXISTS db_mykicks
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE db_mykicks;

-- ---------------------------------------------------------
-- 1. USERS
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS users (
    id              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    role            ENUM('customer', 'kasir', 'admin') NOT NULL,
    full_name       VARCHAR(150) NOT NULL,
    email           VARCHAR(150) UNIQUE,
    phone           VARCHAR(20) UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    created_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                        ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_users_role (role)
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- 2. CUSTOMER ADDRESSES
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS customer_addresses (
    id                  BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    user_id             BIGINT UNSIGNED NOT NULL,
    label               VARCHAR(50) NOT NULL,
    full_address        TEXT NOT NULL,
    latitude            DECIMAL(10,7) NOT NULL,
    longitude           DECIMAL(10,7) NOT NULL,
    distance_km         DECIMAL(5,2) NOT NULL,
    is_within_radius    TINYINT(1) NOT NULL,
    is_default          TINYINT(1) NOT NULL DEFAULT 0,
    created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_address_user FOREIGN KEY (user_id)
        REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_address_user (user_id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- 3. SERVICES
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS services (
    id                  BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    name                VARCHAR(100) NOT NULL,
    description         VARCHAR(255),
    price               DECIMAL(12,2) NOT NULL,
    estimated_hours     INT NOT NULL DEFAULT 24,
    is_active           TINYINT(1) NOT NULL DEFAULT 1,
    created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- 4. ORDERS
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS orders (
    id                  BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    order_code          VARCHAR(20) NOT NULL UNIQUE,
    customer_id         BIGINT UNSIGNED NOT NULL,
    channel             ENUM('online', 'pos') NOT NULL DEFAULT 'online',
    fulfillment_type    ENUM('pickup_delivery', 'drop_off') NOT NULL,
    address_id          BIGINT UNSIGNED NULL,
    distance_km         DECIMAL(5,2) NULL,
    subtotal            DECIMAL(12,2) NOT NULL DEFAULT 0,
    total               DECIMAL(12,2) NOT NULL DEFAULT 0,
    status              ENUM(
                            'pending_payment',
                            'confirmed',
                            'picked_up',
                            'in_progress',
                            'ready',
                            'delivered',
                            'completed',
                            'cancelled'
                        ) NOT NULL DEFAULT 'pending_payment',
    payment_status      ENUM('unpaid', 'paid', 'failed', 'expired', 'refunded')
                            NOT NULL DEFAULT 'unpaid',
    handled_by          BIGINT UNSIGNED NULL,
    notes               VARCHAR(255) NULL,
    created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                            ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_order_customer FOREIGN KEY (customer_id)
        REFERENCES users(id),
    CONSTRAINT fk_order_address FOREIGN KEY (address_id)
        REFERENCES customer_addresses(id),
    CONSTRAINT fk_order_handler FOREIGN KEY (handled_by)
        REFERENCES users(id),
    INDEX idx_order_customer (customer_id),
    INDEX idx_order_status (status),
    INDEX idx_order_created (created_at)
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- 5. ORDER ITEMS
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS order_items (
    id                  BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    order_id            BIGINT UNSIGNED NOT NULL,
    service_id          BIGINT UNSIGNED NOT NULL,
    shoe_description    VARCHAR(255) NULL,
    qty                 INT NOT NULL DEFAULT 1,
    price               DECIMAL(12,2) NOT NULL,
    subtotal            DECIMAL(12,2) NOT NULL,
    CONSTRAINT fk_item_order FOREIGN KEY (order_id)
        REFERENCES orders(id) ON DELETE CASCADE,
    CONSTRAINT fk_item_service FOREIGN KEY (service_id)
        REFERENCES services(id),
    INDEX idx_item_order (order_id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- 6. ORDER STATUS LOG
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS order_status_log (
    id              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    order_id        BIGINT UNSIGNED NOT NULL,
    status          VARCHAR(30) NOT NULL,
    note            VARCHAR(255) NULL,
    changed_by      BIGINT UNSIGNED NULL,
    created_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_log_order FOREIGN KEY (order_id)
        REFERENCES orders(id) ON DELETE CASCADE,
    CONSTRAINT fk_log_user FOREIGN KEY (changed_by)
        REFERENCES users(id),
    INDEX idx_log_order (order_id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- 7. PAYMENTS
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS payments (
    id                  BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    order_id            BIGINT UNSIGNED NOT NULL,
    midtrans_order_id   VARCHAR(100) NOT NULL UNIQUE,
    snap_token          VARCHAR(255) NULL,
    payment_method      VARCHAR(50) NULL,
    gross_amount        DECIMAL(12,2) NOT NULL,
    status              ENUM('pending', 'paid', 'failed', 'expired', 'refunded')
                            NOT NULL DEFAULT 'pending',
    paid_at             TIMESTAMP NULL,
    created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                            ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_payment_order FOREIGN KEY (order_id)
        REFERENCES orders(id),
    INDEX idx_payment_order (order_id)
) ENGINE=InnoDB;

-- ============================================================
-- SEED DATA
-- ============================================================

-- Master layanan
INSERT INTO services (name, description, price, estimated_hours, is_active) VALUES
('Deep Cleaning',  'Pembersihan menyeluruh untuk mengembalikan sepatu seperti baru.',        50000, 24, 1),
('Fast Cleaning',  'Pembersihan cepat untuk sepatu yang hanya kotor ringan.',                30000,  2, 1),
('Unyellowing',    'Perawatan khusus menghilangkan kekuningan pada sol dan bagian putih.',   75000, 48, 1);