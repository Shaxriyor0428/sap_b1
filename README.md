# SAP B1 Service Layer: mock va backend

SAP Business One Service Layer'ning nusxasi (mock) va unga HTTP orqali ulanadigan .NET backend. Real SAP'ga ulanish uchun faqat sozlamalar almashadi, kod o'zgarmaydi.

- Reja: [`PLAN.md`](PLAN.md)
- Service Layer shartnomasi va tushuntirishlar `docs/` papkasida. U private va git'da saqlanmaydi.

> **Holat:** 4-bosqich: mock'da barcha obyektlar, biznes qoidalar, `$expand` va UDF, backend'da mijozlar va hudud bilan ishlash, oziq-ovqat do'koni skripti va SQL vazifalari tayyor. `SQLQueries` keyingi bosqichda qo'shiladi.

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

Jadvallar faqat yangi volume'da yaratiladi. Kodni yangilagach yangi jadval qo'shilgan bo'lsa, masalan `CUFD`, shu buyruqni bir marta ishlating, aks holda mock so'rovlarga 500 qaytaradi.

Python kabi avval IPv6 ni sinaydigan dasturlar Windows'da `localhost` bilan sekin ishlashi mumkin. Bunday holatda `127.0.0.1` yozing. `curl`, Postman va `newman` `localhost` bilan ishlaydi.

## Mock Service Layer obyektlari

| Obyekt | Jadvallar | Amallar |
|---|---|---|
| `BusinessPartners` | `OCRD` | Ro'yxat, olish, yaratish, o'zgartirish, o'chirish |
| `EmployeesInfo` | `OHEM` | Ro'yxat, olish, yaratish, o'zgartirish, o'chirish |
| `Items` | `OITM`, `OITW` | Ro'yxat, olish, yaratish, o'zgartirish, o'chirish. Qoldiq faqat hujjat orqali o'zgaradi |
| `ChartOfAccounts` | `OACT` | Ro'yxat, olish, yaratish, o'zgartirish, o'chirish |
| `Invoices` | `OINV`, `INV1`, `INV6` | Ro'yxat, olish, yaratish, o'zgartirish, `Cancel`, `Close`. O'chirib bo'lmaydi |
| `PurchaseInvoices` | `OPCH`, `PCH1` | Ro'yxat, olish, yaratish, o'zgartirish, `Cancel`, `Close`. O'chirib bo'lmaydi |
| `IncomingPayments` | `ORCT`, `RCT2` | Ro'yxat, olish, yaratish, o'zgartirish, `Cancel`. O'chirib bo'lmaydi |
| `VendorPayments` | `OVPM`, `VPM2` | Ro'yxat, olish, yaratish, o'zgartirish, `Cancel`. O'chirib bo'lmaydi |
| `CompanyService_GetPeriods`, `CompanyService_GetPeriod` | `OACP` | Standart hisoblarni o'qish |
| `UserFieldsMD` | `CUFD` va jadvaldagi `U_` ustun | Ro'yxat, olish, yaratish, o'zgartirish, o'chirish |

`$expand=BusinessPartner` `Invoices`, `PurchaseInvoices` va `IncomingPayments`da ishlaydi, ichida `$select` bilan:

```http
GET /b1s/v2/Invoices(1)?$select=DocEntry,DocNum,DocTotal&$expand=BusinessPartner($select=CardName,Phone1)
```

## UDF: hudud maydoni

```http
POST /b1s/v2/UserFieldsMD
{"Name":"Region","Type":"db_Alpha","Size":50,"Description":"Hudud","SubType":"st_None","TableName":"OCRD"}

PATCH /b1s/v2/BusinessPartners('C0001')
{"U_Region":"Toshkent"}

GET /b1s/v2/BusinessPartners?$filter=U_Region eq 'Toshkent'
```

`Name`da `U_` yozilmaydi, ustun va API maydoni `U_Region` bo'ladi. Mock'da maydon darhol ishlaydi. Real SAP'da Service Layer'ni qayta ishga tushirish kerak bo'lishi mumkin.

## Oziq-ovqat do'koni skripti

Do'konning 10 siklini faqat Service Layer API orqali bajaradi: kassa hisobi, ta'minotchi, mol, xarid, xodim, mijoz, sotuv, kirim to'lov. Oxirida qoldiq = xarid - sotuv va qarz = sotuv - to'lov ekanini API'dan o'qib tekshiradi.

```bash
docker compose down -v
docker compose up --build -d
docker compose run --rm --build grocery-store
```

Hammasi mos kelsa, `ACCEPTED` chiqadi va chiqish kodi 0 bo'ladi. Farq bo'lsa 1, skript ishlay olmasa 2.

Kodlar takrorlanmasligi kerak, shuning uchun ikkinchi marta ishga tushirish uchun bazani tozalang yoki `.env`da `GROCERY_PREFIX`ni o'zgartiring.

Real SAP'da ishga tushirish uchun `.env`da `SL_USERNAME`, `SL_PASSWORD`, `SL_WAREHOUSE`, `SL_TAX_CODE` yoki `SL_VAT_GROUP`, `GROCERY_CASH_PARENT_ACCOUNT` va `GROCERY_CURRENT_MONTH`ni ham to'ldiring: `.env.example`ga qarang.

## SQL vazifalari

`sql/` papkasida 14 ta fayl. Har faylning boshida vazifa matni va do'kon ma'lumotidagi kutilgan natija bor. Ular faqat do'kon ma'lumotiga mo'ljallangan: Postman qoldirgan hujjatlar natijani buzadi, shuning uchun bazani avval tozalang.

```bash
docker compose down -v
docker compose up --build -d
docker compose run --rm --build grocery-store
bash sql/check.sh
```

`check.sh` 1-13-fayllarni mock bazasida ishga tushirib, natijani fayl boshidagi `-- > ` qatorlari bilan solishtiradi. Bitta faylni ishga tushirish:

```bash
docker compose exec -T db psql -U sap_mock -d sap_mock < sql/01_sales_over_500.sql
```

| Fayl | Vazifa |
|---|---|
| `01`, `02` | 500 dan katta sotuvlar, mijoz ismi bilan |
| `03`, `04`, `05` | Shu oydagi sotuvlar, kelgan pul, sotuvlar soni |
| `06` | Sotuvlar va mollar |
| `07`, `08`, `09` | Eng qimmat mol, eng qimmat 10 ta mol, mijoz ismi bilan |
| `10`, `11` | Eng ko'p marta xarid qilgan va eng ko'p pul sarflagan 10 ta mijoz |
| `12` | Xaridlar va qatorlar |
| `13` | Birorta ham sotuvi bo'lmagan mijozlar (`LEFT JOIN`) |
| `14` | 3-5-vazifalar `[%0]` va `[%1]` bilan. Mock'da ishlamaydi, SAP B1 Query Manager uchun |

Bazadagi qiymatlar Service Layer'dagidan farq qiladi: `CardType` bazada `'C'`, API'da `cCustomer`. So'rovlar HANA'ga mos yozilgan, lekin faqat PostgreSQL'da tekshirilgan.

## Backend API

| So'rov | Vazifasi |
|---|---|
| `POST /api/login` | Kirish: `{"userName":"manager","password":"1234"}`, token qaytaradi |
| `POST /api/logout` | Chiqish |
| `GET /api/customers` | Ro'yxat. Parametrlar: `name`, `phone`, `address`, `region`, `page`, `pageSize` (standart 20, eng ko'pi 100). `region` aniq taqqoslanadi: `U_Region eq 'Toshkent'` |
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

Testlar o'z ma'lumotlarini `PM` va `PD` prefikslari bilan yaratadi. `PM` ma'lumotlari oxirida o'chiriladi. `PD` hujjatlari esa qoladi, chunki hujjatni o'chirib bo'lmaydi, lekin har ishga tushirish yangi kodlar ishlatadi, shuning uchun to'plamni qayta ishga tushirish mumkin.

Mock environment'ida hisob-kitoblar soliqsiz. Real SAP'da soliq bor bo'lsa, `07 Documents and payments` papkasidagi aniq summalar farq qilishi mumkin.

`08 Expand and UDF` papkasi `U_Region`ni yaratadi va u bazada qoladi. Sinov maydonlarini esa o'zi o'chiradi. Real SAP'da yangi maydon Service Layer qayta ishga tushmaguncha ko'rinmasligi mumkin, bunda birinchi ishga tushirishda shu papkaning maydonni yozadigan so'rovlari tushadi. `IncomingPayments` uchun `$expand` real SAP'da rad etilishi mumkin: bu hujjat talabi, EDMX'da esa bunday bog'lanish yo'q.

SQL vazifalari faqat do'kon ma'lumotlariga mo'ljallangan, shuning uchun ulardan oldin bazani tozalab, faqat do'kon skriptini ishga tushiring.

## Ishlab chiqish

```bash
dotnet build SapB1.slnx
dotnet test SapB1.slnx
dotnet format SapB1.slnx --verify-no-changes
```
