# SAP B1 Service Layer: mock va backend

SAP Business One Service Layer'ning nusxasi (mock) va unga HTTP orqali ulanadigan .NET backend. Real SAP'ga ulanish uchun faqat sozlamalar almashadi, kod o'zgarmaydi.

- Reja: [`PLAN.md`](PLAN.md)
- Service Layer shartnomasi va tushuntirishlar `docs/` papkasida. U private va git'da saqlanmaydi.

> **Holat:** 1B bosqichi: mock'da login, mijozlar va OData, backend'da mijozlar bilan ishlash tayyor. Qolgan obyektlar keyingi bosqichlarda qo'shiladi.

## Kerakli dasturlar

- Docker Desktop
- Ishlab chiqish uchun: .NET 10 SDK; Postman testlarini buyruq qatoridan ishga tushirish uchun Node.js

## Ishga tushirish

```bash
docker compose up --build
```

`.env` fayli shart emas: `docker-compose.yml`da standart qiymatlar bor. Ularni o'zgartirish uchun `.env.example`ni `.env` nomi bilan nusxalang.

| Servis | Manzil |
|---|---|
| Backend ilova | `http://localhost:8080/api` |
| Mock Service Layer | `http://localhost:50000/b1s/v2/` |
| PostgreSQL | `localhost:5433` |

Mock foydalanuvchilari: `manager` / `1234` (superuser), `user1` / `1234` (oddiy). Kompaniya bazasi: `TEST`.

Bazani toza holatga qaytarish:

```bash
docker compose down -v
```

## Backend API

| So'rov | Vazifasi |
|---|---|
| `POST /api/login` | Kirish: `{"userName":"manager","password":"1234"}`, token qaytaradi |
| `POST /api/logout` | Chiqish |
| `GET /api/customers` | Ro'yxat. Parametrlar: `name`, `phone`, `address`, `page`, `pageSize` (standart 20, eng ko'pi 100) |
| `POST /api/customers` | Yaratish: `code`, `name`, `phone`, `cellular`, `email`, `address`, `city` |
| `PATCH /api/customers/{code}` | O'zgartirish: faqat yuborilgan maydonlar o'zgaradi |
| `DELETE /api/customers/{code}` | O'chirish |

Login'dan boshqa so'rovlar uchun `Authorization: Bearer <token>` sarlavhasi kerak.

```bash
curl -X POST http://localhost:8080/api/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"manager","password":"1234"}'

curl "http://localhost:8080/api/customers?name=Ali&page=1&pageSize=20" \
  -H "Authorization: Bearer <token>"
```

Ro'yxat javobi:

```json
{ "items": [], "page": 1, "pageSize": 20, "hasNextPage": false }
```

Xato javobi: Service Layer xatosi shu status, kod va xabar bilan qaytariladi.

```json
{ "code": "-10", "message": "1320000140 - Business partner code 'C0001' already assigned ..." }
```

Sessiya tugasa, API `401` va `SESSION_EXPIRED` qaytaradi: qayta login qilish kerak.

## Real SAP'ga ulash

Kod o'zgarmaydi. `.env`da shu qiymatlarni o'zgartiring:

```bash
SL_BASE_URL=https://<server>:50000/b1s/v2/
SL_COMPANY_DB=<kompaniya bazasi>
SL_SKIP_TLS_VERIFY=true
```

Faqat backend'ni ishga tushiring, login va parol `POST /api/login` orqali beriladi:

```bash
docker compose up --build api
```

`SL_SKIP_TLS_VERIFY=true` self-signed sertifikat uchun kerak, faqat test muhitida ishlating.

## Postman

`postman/` papkasidagi to'plam va environment'larni Postman'ga import qiling (File -> Import) va to'plamni tartib bilan ishga tushiring.

| Environment | Vazifasi |
|---|---|
| `mock` | Lokal mock uchun, tayyor |
| `real-sap` | Real SAP uchun: `baseUrl`, `CompanyDB`, `UserName`, `Password` to'ldiriladi. Self-signed sertifikat tufayli Postman'da SSL tekshiruvini o'chiring. `05 Backend API` papkasi uchun backend ilova ham real SAP sozlamalari bilan ishlab turishi kerak |

Buyruq qatoridan:

```bash
npx newman run postman/SapB1.postman_collection.json -e postman/mock.postman_environment.json
```

Testlar o'z ma'lumotlarini `PM` prefiksi bilan yaratadi va oxirida o'chiradi.

## Ishlab chiqish

```bash
dotnet build SapB1.slnx
dotnet test SapB1.slnx
dotnet format SapB1.slnx --verify-no-changes
```
