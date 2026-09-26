# Database migrations

## 002_add_security_stamp.sql

Apply after `001_create_payments.sql` (or independently - it only touches `users`). Adds `users.security_stamp`, which JWT validation checks on every request to support real token revocation (logout, disabling an account). Running `schema.sql` alone on a fresh database is enough - this migration is only needed to bring an **existing** database that already has a `users` table up to date, since `schema.sql` uses `CREATE TABLE IF NOT EXISTS` and won't alter a table that already exists.

# Midtrans Snap migration and configuration

The existing database schema is not included in this repository. Before applying `001_create_payments.sql`, verify that `orders.id` is a signed `BIGINT` and that `orders` uses InnoDB so the foreign key can be created. Adjust the migration if the live schema differs.

Apply the migration once, after the existing application tables are present. It creates `payments` for each payment attempt and `payment_events` for payment status transitions.

Configure the Midtrans Sandbox Server Key outside source control:

```powershell
dotnet user-secrets set "Midtrans:ServerKey" "SB-Mid-server-..."
dotnet user-secrets set "Midtrans:IsProduction" "false"
```

For a deployed environment, provide `Midtrans__ServerKey` and `Midtrans__IsProduction` as environment variables. Keep `IsProduction` false while using Sandbox credentials. `Midtrans:ExpiryMinutes` defaults to 1440 minutes and controls the expiry sent to Snap.

Set the Midtrans Payment Notification URL to `https://<your-host>/payments/midtrans/notification`. The endpoint must be reachable over HTTPS. After a payment is started, the authenticated customer receives `redirectUrl`; the server treats the signed notification plus the Midtrans Get Status API response as the source of truth.

## Endpoints

- `POST /orders/{orderId}/payments` starts a Snap checkout or returns the current active checkout.
- `GET /orders/{orderId}/payments/latest` returns the latest payment status for the owning customer.
- `POST /payments/midtrans/notification` receives Midtrans notifications.
