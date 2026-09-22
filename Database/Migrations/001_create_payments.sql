-- Apply after the existing MyKicksBuddy tables have been created.
-- Assumes orders.id is BIGINT and the orders table uses InnoDB.
-- Snap sessions are explicitly configured with a 24-hour expiry by default.
CREATE TABLE payments (
    id BIGINT NOT NULL AUTO_INCREMENT,
    order_id BIGINT NOT NULL,
    provider_order_id VARCHAR(50) NOT NULL,
    transaction_id VARCHAR(100) NULL,
    gross_amount DECIMAL(12, 2) NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'IDR',
    status VARCHAR(24) NOT NULL DEFAULT 'creating',
    snap_token VARCHAR(255) NULL,
    redirect_url VARCHAR(1024) NULL,
    payment_type VARCHAR(50) NULL,
    fraud_status VARCHAR(20) NULL,
    expires_at DATETIME NULL,
    paid_at DATETIME NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uq_payments_provider_order_id (provider_order_id),
    KEY ix_payments_order_id_created_at (order_id, created_at),
    CONSTRAINT fk_payments_order
        FOREIGN KEY (order_id) REFERENCES orders (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE payment_events (
    id BIGINT NOT NULL AUTO_INCREMENT,
    payment_id BIGINT NOT NULL,
    previous_status VARCHAR(24) NOT NULL,
    new_status VARCHAR(24) NOT NULL,
    provider_status VARCHAR(32) NOT NULL,
    status_code VARCHAR(10) NOT NULL,
    transaction_id VARCHAR(100) NULL,
    payment_type VARCHAR(50) NULL,
    fraud_status VARCHAR(20) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY ix_payment_events_payment_id_created_at (payment_id, created_at),
    CONSTRAINT fk_payment_events_payment
        FOREIGN KEY (payment_id) REFERENCES payments (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
