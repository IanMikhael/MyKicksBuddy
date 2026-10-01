# MyKicksBuddy

ASP.NET Core 8 application for shoe-care orders with MySQL and Midtrans Sandbox payment integration.

## Run with Docker

1. Create `.env` from `.env.example` if it does not exist.
2. Fill `MIDTRANS_SERVER_KEY` with a Midtrans Sandbox Server Key.
3. Start the stack:

```powershell
docker compose up --build -d
```

Open `http://localhost:8080/`. MySQL is exposed on `localhost:3307` for local database tools.

Stop the containers with:

```powershell
docker compose down
```

The `mysql_data` volume keeps application data, while `data_protection_keys` keeps login sessions valid across web-container restarts.

## Development data

The Development environment creates this idempotent test data:

- Customer: `customer@mykicksbuddy.local` / `Sandbox123!`
- Admin: `admin@mykicksbuddy.local` / `Admin123!`
- Order: `MKC-SANDBOX-001` for IDR 60,000

Use the upper section of the Payment Playground for the database-backed order checkout. The lower section provides disposable Snap, bank VA, QRIS, and GoPay tests against Midtrans Sandbox.

## Database

MySQL initializes new volumes using:

- `Database/Schema/001_base_schema.sql`
- `Database/Migrations/001_create_payments.sql`
- `Database/Seed/001_services.sql`

The main payment tables are `payments` and `payment_events`. A successful verified Midtrans notification changes `payments.status`, records the transition in `payment_events`, marks the order as paid, and confirms an order that is still waiting for payment.

See `Database/Migrations/README.md` for endpoints and webhook setup.
