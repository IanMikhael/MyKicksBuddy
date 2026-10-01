INSERT INTO services (name, description, price, estimated_hours, is_active) VALUES
    ('Fast Clean', 'Pembersihan ringan bagian upper dan midsole.', 35000, 4, TRUE),
    ('Deep Clean', 'Pembersihan menyeluruh upper, midsole, outsole, dan insole.', 60000, 24, TRUE),
    ('Unyellowing', 'Perawatan untuk mengurangi warna kuning pada midsole.', 85000, 48, TRUE)
ON DUPLICATE KEY UPDATE
    description = VALUES(description),
    price = VALUES(price),
    estimated_hours = VALUES(estimated_hours),
    is_active = VALUES(is_active);
