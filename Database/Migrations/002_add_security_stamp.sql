-- Apply after the existing MyKicksBuddy `users` table is present.
-- Adds a revocation handle for JWTs: every authenticated request re-checks
-- this value against the token's own copy, so logout / disabling an account
-- can invalidate already-issued tokens instead of waiting out their expiry.
ALTER TABLE users
    ADD COLUMN security_stamp VARCHAR(64) NOT NULL DEFAULT '' AFTER password_hash;

-- Backfill existing rows with a real stamp - a shared '' would let their old
-- tokens (which have no sstamp claim from before this migration) collide,
-- and would also match each other if two accounts were both left blank.
UPDATE users SET security_stamp = UUID() WHERE security_stamp = '';
