# Database and Midtrans configuration

The Docker setup now initializes a MySQL 8.4 database in this order:

1. `Database/Schema/001_base_schema.sql` creates users, services, addresses, orders, order items, and order status history.
2. `Database/Migrations/001_create_payments.sql` creates payment attempts and payment status events.
3. `Database/Seed/001_services.sql` inserts the initial service catalog.

The SQL initialization files run only when Docker creates a new `mysql_data` volume. Schema changes after that point should be added as a new numbered migration and applied to existing databases.

Start the complete development stack from the repository root:

```powershell
Copy-Item .env.example .env
docker compose up --build -d
```

Open `http://localhost:8080/`. MySQL is also available to local tools at `localhost:3307`. The default development database name, user, and password are documented in `.env.example` and can be overridden there.

In Development, the application creates idempotent test data after the database becomes healthy:

- Customer: `customer@mykicksbuddy.local` / `Sandbox123!`
- Admin: `admin@mykicksbuddy.local` / `Admin123!`
- Order: `MKC-SANDBOX-001`, with a total of IDR 60,000

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

## Development playground

When `ASPNETCORE_ENVIRONMENT=Development`, open `http://localhost:8080/` to use the Payment Playground. The order checkout section logs in with the seeded customer, loads the customer's order from MySQL, and creates an idempotent Snap payment in `payments`. The provider status notification is recorded in `payment_events` and updates the related order.

The lower playground can create disposable Snap, Core API virtual account, QRIS, and GoPay transactions for provider testing. Those disposable transactions do not write to the application database, and their controller is disabled outside Development.

To receive real Sandbox payment notifications, expose the application over public HTTPS and set the Midtrans Payment Notification URL to `https://<your-host>/payments/midtrans/notification`.
