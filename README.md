# Stock

ระบบร้านค้าที่เน้นการจัดการ stock: จัดการสินค้าและยอดคงเหลือ, ตะกร้าแบบ guest, checkout ที่จอง stock, และชำระเงินแล้วตัด stock จริง

**ไม่มีระบบ login/auth** (ตั้งใจตัดออก) ใครเข้า `/admin` ก็ใช้งานได้ ห้ามนำไปใช้งานจริงโดยไม่เพิ่มการยืนยันตัวตน

## Stack

| ส่วน | เทคโนโลยี |
|---|---|
| Frontend | Next.js 15 (App Router), TypeScript, Tailwind CSS |
| Backend | ASP.NET Core 8 Web API, EF Core 8 (Npgsql) |
| Database | PostgreSQL 16 (Docker) |

## โครงสร้างโปรเจกต์

```
Stock/
├─ api/                  # ASP.NET Core Web API
│  ├─ Controllers/       # products, stocks, cart, checkout, orders, payments, mock provider
│  ├─ Domain/            # entities
│  ├─ Data/              # AppDbContext + EF migrations
│  ├─ Services/          # OrderService, PaymentService, IPaymentGateway, ExpiredOrderSweeper
│  └─ Dtos/
├─ web/                  # Next.js
│  └─ app/
│     ├─ (shop)/         # หน้าร้าน: /, /cart, /checkout, /orders/[id], /pay/[ref]
│     └─ admin/          # /admin/products, /admin/stock, /admin/orders
├─ docker-compose.yml    # Postgres (+ pgAdmin แบบ optional)
├─ .env.example
└─ global.json           # ล็อก .NET SDK 8
```

## สิ่งที่ต้องมี

- Docker + Docker Compose
- .NET SDK 8 (ดู `global.json`)
- Node.js 20+ และ npm

## เริ่มใช้งาน

### 1. ฐานข้อมูล

```bash
cp .env.example .env        # แก้รหัสผ่านใน .env ตามต้องการ
docker compose up -d db
```

pgAdmin (ไม่บังคับ): `docker compose --profile tools up -d` แล้วเปิด http://localhost:5050

### 2. API (พอร์ต 5003)

Connection string, `Payments:MockEnabled` และ `Payments:WebhookSecret` อยู่ใน `api/appsettings.Development.json` ต้องรันในโหมด Development และรหัสผ่านใน connection string ต้องตรงกับ `.env`

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update --project api
ASPNETCORE_ENVIRONMENT=Development dotnet run --project api --urls http://localhost:5003
```

บน PowerShell ใช้ `$env:ASPNETCORE_ENVIRONMENT = "Development"` แทน

Swagger: http://localhost:5003/swagger

### 3. Web (พอร์ต 3000)

```bash
cd web
npm install
npm run dev
```

Next.js proxy `/api/*` ไปที่ API ผ่าน rewrite (ตั้ง URL ได้ด้วยตัวแปร `API_URL` ค่าเริ่มต้น `http://localhost:5003`) จึงไม่ต้องตั้ง CORS และ cookie ของตะกร้าอยู่ใน origin เดียวกับหน้าเว็บ

- หน้าร้าน: http://localhost:3000
- Admin: http://localhost:3000/admin/products

> อย่ารัน `npm install` สองตัวพร้อมกันใน `web/` บน Windows จะทำให้ `node_modules` เสีย

## ข้อมูลและโมเดล stock

| ตาราง | หน้าที่ |
|---|---|
| `products` | sku (unique), ชื่อ, ราคา, หมวด, รูป, `is_active` |
| `stocks` | ต่อ sku: `quantity_on_hand`, `quantity_reserved` (**available = on_hand − reserved**) |
| `stock_movements` | ประวัติการเปลี่ยนแปลงทุกครั้ง |
| `carts`, `cart_items` | ตะกร้า guest |
| `orders`, `order_items` | order พร้อม snapshot ชื่อและราคา ณ ตอนซื้อ |
| `payments`, `payment_events` | ความพยายามจ่ายเงิน และ event id ที่ประมวลผลแล้ว |

CHECK constraint ใน DB กัน `on_hand < 0` และ `reserved > on_hand` อีกชั้นหนึ่ง

### Flow การขาย

1. **Cart**: เก็บ id ใน cookie `cart_id` (HttpOnly) ตะกร้า**ไม่จอง stock**
2. **Checkout**: ใน transaction เดียว ล็อกแถว cart, จอง stock ทีละรายการแบบ conditional UPDATE (`reserved += n` เมื่อ `on_hand − reserved >= n`), สร้าง order สถานะ `Pending` และล้าง cart ถ้ารายการใดไม่พอ ยกเลิกทั้งหมดและตอบ 409
3. **Pending** อยู่ได้ตามเวลาที่ตั้ง (ค่าเริ่มต้น 15 นาที) ถ้าไม่จ่ายหรือยกเลิก `ExpiredOrderSweeper` จะเปลี่ยนเป็น `Expired` และคืนของ
4. **จ่ายสำเร็จ** (ผ่าน webhook): order เป็น `Paid`, `on_hand` และ `reserved` ลดลงเท่ากัน (available ไม่เปลี่ยน)
5. **ยกเลิก**: `Pending → Cancelled` และคืน reservation

การเปลี่ยนสถานะ order ทุกแบบ (จ่าย / ยกเลิก / หมดอายุ) ใช้ `UPDATE ... WHERE status = 'Pending'` แล้วดูจำนวนแถวที่โดน เพื่อให้ฝ่ายที่แข่งกัน ชนะได้เพียงฝ่ายเดียว

ใน `stock_movements` ค่า `change` ของ `Reserve` (ลบ) และ `Release` (บวก) คือการเปลี่ยนของ **available** ส่วน `Sale` (ลบ) คือการเปลี่ยนของ **on hand**

## Payment

ปัจจุบันใช้ **mock provider** (ไม่มีการเรียก provider ภายนอก)

- `POST /api/orders/{id}/pay` สร้าง payment และส่ง URL หน้าจ่ายเงินกลับมา (ถ้ามี payment ที่ยังเปิดอยู่จะใช้ตัวเดิม)
- หน้า `/pay/{ref}` เป็นหน้าจำลอง provider มีปุ่ม "Pay successfully" / "Simulate failure" ซึ่งเรียก `POST /api/mock-provider/payments/{ref}/simulate` ให้ทำหน้าที่แทน provider ที่ส่งผลมาเข้า `PaymentService.ProcessAsync` ตัวเดียวกับ webhook
- endpoint ของ mock provider ตอบ 404 เว้นแต่ `Payments:MockEnabled = true`
- **Webhook** `POST /api/payments/webhook`
  - ต้องมี header `X-Signature: sha256=<hex>` เป็น HMAC-SHA256 ของ raw body ด้วย `Payments:WebhookSecret`
  - body: `{"eventId":"...","type":"payment.succeeded|payment.failed","providerRef":"...","amount":100}`
  - **Idempotent**: `eventId` ถูกบันทึกใน transaction เดียวกับการเปลี่ยนสถานะ ส่งซ้ำได้ผล `Duplicate` และไม่เปลี่ยนอะไร
  - จำนวนเงินไม่ตรง → 422, ไม่รู้จัก payment → 404
  - ถ้าเงินเข้ามาหลัง order ถูกยกเลิกหรือหมดอายุ stock ไม่ถูกแตะ และ payment ถูกติดสถานะ `RefundRequired` (ยังไม่มีขั้นตอนคืนเงิน)

เปลี่ยนไปใช้ provider จริง: เขียน class ที่ implement `IPaymentGateway` แล้วเปลี่ยนการลงทะเบียนใน `api/Program.cs` รูปแบบลายเซ็นของ provider จริงจะต่างจากแบบที่ใช้ที่นี่ ต้องปรับตัวตรวจใน `PaymentsController`

ตัวอย่างยิง webhook เอง (bash):

```bash
BODY='{"eventId":"evt_1","type":"payment.succeeded","providerRef":"mock_xxx","amount":100}'
SIG=$(printf '%s' "$BODY" | openssl dgst -sha256 -hmac "dev-only-webhook-secret-change-me" -hex | sed 's/^.* //')
curl -X POST http://localhost:5003/api/payments/webhook \
  -H 'Content-Type: application/json' -H "X-Signature: sha256=$SIG" -d "$BODY"
```

## API สรุป

| Method | Path | หน้าที่ |
|---|---|---|
| GET/POST | `/api/products` | รายการ (ค้นหา, แบ่งหน้า) / สร้างสินค้า + stock เริ่มต้น |
| GET/PUT/DELETE | `/api/products/{sku}` | ดู / แก้ / ปิดการใช้งาน (soft delete) |
| GET | `/api/stocks`, `/api/stocks/{sku}` | ดูยอด (`lowStockOnly` กรองของใกล้หมด) |
| POST | `/api/stocks/{sku}/adjust` | ปรับยอด On hand (In / Out / Adjust) |
| GET | `/api/stocks/{sku}/movements` | ประวัติ |
| GET | `/api/cart` | ดูตะกร้า |
| POST | `/api/cart/items` | เพิ่มสินค้า |
| PUT/DELETE | `/api/cart/items/{sku}` | แก้จำนวน / ลบ |
| POST | `/api/checkout` | สร้าง order + จอง stock |
| GET | `/api/orders`, `/api/orders/{id}` | รายการ (admin) / ดู order ด้วย Guid |
| POST | `/api/orders/{id}/cancel` | ยกเลิก (เฉพาะ Pending) |
| POST | `/api/orders/{id}/pay` | เริ่มจ่ายเงิน |
| POST | `/api/payments/webhook` | รับผลจาก provider (ต้องเซ็น) |

## ตั้งค่า (appsettings / environment)

| คีย์ | ค่าเริ่มต้น | ความหมาย |
|---|---|---|
| `ConnectionStrings:Default` | ใน `appsettings.Development.json` | Postgres |
| `Orders:ReservationMinutes` | 15 | เวลาที่จอง stock ไว้ให้ order Pending |
| `Orders:SweepIntervalSeconds` | 60 | ความถี่ตรวจ order หมดอายุ |
| `Payments:MockEnabled` | false (Development = true) | เปิด mock provider |
| `Payments:WebhookSecret` | (Development ตั้งค่าไว้ให้) | ถ้าว่าง webhook ปฏิเสธทุกคำขอ (503) |

ตั้งผ่าน environment ได้ เช่น `Orders__ReservationMinutes=1`

## ข้อจำกัดที่ควรรู้

- **ไม่มี auth**: `/admin/*` และ `GET /api/orders` (มีชื่อและอีเมลลูกค้า) เปิดสาธารณะ
- ใครรู้ลิงก์ order (Guid) ก็ดูและยกเลิกได้ ระบบ guest ไม่มีวิธีอื่นยืนยันตัวตน
- ยังไม่มีการคืนเงินและหน้า admin ดู payment / `RefundRequired`
- ไม่มี automated test: ที่ผ่านมาทดสอบด้วยมือผ่าน curl (race, webhook ซ้ำ, หมดอายุ) และเบราว์เซอร์
- ไม่มี seed data และไม่ migrate อัตโนมัติ ต้องรัน `dotnet ef database update` เอง
- ยังไม่มี Dockerfile สำหรับ API/web และ CI
