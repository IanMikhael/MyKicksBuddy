# MyKicksBuddy — Backend API

Sistem informasi toko cuci sepatu berbasis web dengan payment gateway dan chatbot AI terintegrasi.

## Tech Stack

- **Framework**: .NET 8 Core MVC
- **ORM**: Dapper (Micro-ORM)
- **Database**: MySQL 8
- **Payment Gateway**: Midtrans
- **AI Workflow**: n8n + Groq LLM
- **Auth**: Cookie-based Authentication

---

## Setup Lokal

### Prasyarat
- .NET 8 SDK
- MySQL 8 (via Laragon atau XAMPP)
- n8n (untuk workflow chatbot)

### Langkah Setup

**1. Clone repo**
```bash
git clone https://github.com/<username>/MyKicksBuddy.git
cd MyKicksBuddy
```

**2. Import database**
```bash
mysql -u root -p < schema.sql
```

**3. Set secrets via dotnet user-secrets**
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=db_mykicks;User=root;Password=;"
dotnet user-secrets set "ChatbotApiKey" "isi-api-key-kamu"
```

**4. Jalankan aplikasi**
```bash
dotnet run
```

App akan berjalan di `http://localhost:5219`.

---

## Struktur Folder

```
MyKicksBuddy/
├── Controllers/          — HTTP endpoint handlers
│   ├── AuthController.cs
│   ├── AddressesController.cs
│   ├── OrdersController.cs
│   ├── StaffOrdersController.cs
│   └── ChatbotController.cs
├── Models/
│   ├── Entities/         — Representasi tabel database
│   └── Dtos/             — Request & response shape
├── Repositories/         — Query Dapper ke MySQL
├── Services/             — Business logic
├── Filters/              — API Key middleware
├── schema.sql            — Skema database + seed data
└── Program.cs            — Konfigurasi aplikasi
```

---

## Endpoint API

### Auth
| Method | Endpoint | Akses | Keterangan |
|--------|----------|-------|------------|
| POST | `/auth/register` | Public | Registrasi customer baru |
| POST | `/auth/login` | Public | Login, set cookie session |
| POST | `/auth/logout` | Public | Hapus cookie session |

### Addresses (Customer)
| Method | Endpoint | Akses | Keterangan |
|--------|----------|-------|------------|
| GET | `/addresses` | Customer | List alamat tersimpan |
| POST | `/addresses` | Customer | Tambah alamat baru + validasi radius 5 km |
| DELETE | `/addresses/{id}` | Customer | Hapus alamat |

### Orders (Customer)
| Method | Endpoint | Akses | Keterangan |
|--------|----------|-------|------------|
| POST | `/orders` | Customer | Buat pesanan baru |
| GET | `/orders` | Customer | Daftar pesanan milik customer |
| GET | `/orders/{id}` | Customer | Detail pesanan + items |

### Staff Orders (Kasir/Admin)
| Method | Endpoint | Akses | Keterangan |
|--------|----------|-------|------------|
| PATCH | `/staff/orders/{id}/status` | Kasir, Admin | Update status pesanan |
| GET | `/staff/orders/{id}/logs` | Kasir, Admin | Timeline log status |
| GET | `/staff/orders/{id}` | Kasir, Admin | Detail pesanan lengkap |

### Chatbot (API Key)
| Method | Endpoint | Akses | Keterangan |
|--------|----------|-------|------------|
| GET | `/api/chatbot/services` | API Key | Daftar layanan aktif |
| GET | `/api/chatbot/orders/{code}` | API Key | Cek status pesanan by kode |
| POST | `/api/chatbot/estimate` | API Key | Estimasi harga & waktu |

> Endpoint chatbot memerlukan header `X-Api-Key: <nilai dari ChatbotApiKey secret>`.

---

## Roles

| Role | Akses |
|------|-------|
| `customer` | Register, login, kelola alamat, buat & lihat pesanan sendiri |
| `kasir` | Update status pesanan, lihat detail & log semua pesanan |
| `admin` | Semua akses kasir + manajemen sistem |

---

## Environment Variables (Production)

Saat deploy ke VPS, set environment variable berikut di systemd unit file:

```ini
[Service]
Environment=ConnectionStrings__Default=Server=localhost;Database=db_mykicks;User=root;Password=xxx;
Environment=ChatbotApiKey=isi-api-key-production
```

---

## Catatan Pengembangan

- Auth saat ini menggunakan cookie-based session. Migrasi ke JWT direncanakan pada iterasi berikutnya.
- Real-time tracking status pesanan menggunakan polling interval 10 detik di sisi frontend.
- Chatbot menggunakan n8n + Groq LLM dengan System Prompt berbasis business rules toko.
- Endpoint chatbot tidak mengekspos `customerId`, `notes`, atau detail alamat customer.