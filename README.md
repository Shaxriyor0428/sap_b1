# SAP B1 Service Layer: mock va backend

SAP Business One Service Layer'ning nusxasi (mock) va unga HTTP orqali ulanadigan .NET backend. Real SAP'ga ulanish uchun faqat sozlamalar almashadi, kod o'zgarmaydi.

- Reja: [`PLAN.md`](PLAN.md)
- Service Layer shartnomasi: [`docs/sl-contract.md`](docs/sl-contract.md)
- Har bosqich tushuntirishi: [`docs/explain/`](docs/explain/)

> **Holat:** 1A bosqichi: mock'da login, mijozlar va OData ishlaydi. Backend ilova keyingi bosqichda qo'shiladi.

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
| Mock Service Layer | `http://localhost:50000/b1s/v2/` |
| PostgreSQL | `localhost:5433` |

Mock foydalanuvchilari: `manager` / `1234` (superuser), `user1` / `1234` (oddiy). Kompaniya bazasi: `TEST`.

Bazani toza holatga qaytarish:

```bash
docker compose down -v
```

## Tezkor tekshiruv

```bash
curl -i -X POST http://localhost:50000/b1s/v2/Login \
  -d '{"CompanyDB":"TEST","UserName":"manager","Password":"1234"}'
```

Javobdagi `B1SESSION` cookie'sini keyingi so'rovlarga qo'shing:

```bash
curl "http://localhost:50000/b1s/v2/BusinessPartners?\$top=5" \
  -H "Cookie: B1SESSION=<SessionId>"
```

## Postman

`postman/` papkasidagi to'plam va environment'larni Postman'ga import qiling (File -> Import) va to'plamni tartib bilan ishga tushiring.

| Environment | Vazifasi |
|---|---|
| `mock` | Lokal mock uchun, tayyor |
| `real-sap` | Real SAP uchun: `baseUrl`, `CompanyDB`, `UserName`, `Password` to'ldiriladi. Self-signed sertifikat tufayli Postman'da SSL tekshiruvini o'chiring |

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
