# Service Layer shartnomasi

Mock va backend qanday xulqqa tayanayotgani, u qayerdan olingani va nimasi hali tasdiqlanmagani shu yerda yoziladi.

**Holat:** 5-bosqich, barcha bosqichlar yozilgan. Real SAP'da hali tekshirilmagan: mentor uchun ro'yxat 10-bo'limda. Barcha obyektlar uchun namunalar 7-bo'limda, backend'ning Service Layer'dan nima kutishi 8-bo'limda, do'kon skriptining kutishi 9-bo'limda.

Belgilar: **PDF s.N** — "Working with SAP Business One Service Layer" PDF sahifasi. **EDMX** — `b1-api-cookbook/schemas/metadata.xml` (OData v4). **SDK** — SDK Database Tables Reference.

## 1. Manbalar

| Manba | Holat |
|---|---|
| [Working with SAP Business One Service Layer (PDF)](https://help.sap.com/doc/fc2f5477516c404c8bf9ad1315a17238/10.0/en-US/Working_with_SAP_Business_One_Service_Layer.pdf) | Yuklab olindi (250 s.). O'qildi: login, CRUD, actions, query options, associations, UDF, SQL Query, ETag, limitations |
| [Service Layer API Reference](https://help.sap.com/doc/056f69366b5345a386bb8149f1700c19/10.0/en-US/Service%20Layer%20API%20Reference.html) | Ochiladi (3.8 MB), hali tahlil qilinmagan. Hozircha EDMX ishlatildi |
| [SDK Database Tables Reference](https://help.sap.com/doc/089315d8d0f8475a9fc84fb919b501a3/10.0/en-US/SDKHelp/OSQL.html) | 16 ta jadval o'qildi: OCRD, OUSR, OHEM, OITM, OITW, OINV, INV1, INV6, ORCT, RCT2, OVPM, OPCH, PCH1, OACT, OACP, OSQL |
| [B1 API Cookbook](https://github.com/Xaypanya/b1-api-cookbook) | EDMX va `services/` tahlil qilindi. `services/`da `SQLQueries` yo'q |
| [SAP Learning: SQL Queries](https://learning.sap.com/courses/leveraging-the-sap-business-one-service-layer/managing-sql-queries-using-service-layer-api) | Veb-yig'ish orqali qisman o'qildi |
| [SAP Learning: Introducing](https://learning.sap.com/courses/leveraging-the-sap-business-one-service-layer/introducing-service-layer-api-terms-and-documentation) | O'qilmagan |
| [Apideck guide](https://www.apideck.com/blog/sap-business-one-api-integration-guide) | Veb-yig'ish orqali o'qildi |
| [SAP Community: SQL Query](https://groups.community.sap.com/t5/enterprise-resource-planning-blogs-by-sap/new-sap-business-one-service-layer-sql-query/bc-p/13463010) | Ochilmadi (403). Mavzu PDF 4-bo'limida bor |
| [SAP Answers: Query Manager parametrlari](https://answers.sap.com/t5/enterprise-resource-planning-q-a/sbo-hana-query-with-user-parameters/qaq-p/12574552) | Ochilmadi (403). Faqat qidiruv qisqacha mazmuni ko'rildi |

## 2. Umumiy qoidalar

### URL

- Asos: `https://<server>:50000/b1s/v2/` (PDF s.15; v1 OData 3, v2 OData 4).
- Kalit: matn `('c1')` yoki `(CardCode='c1')`; son `(22)` yoki `(DocEntry=22)`; kompozit `(TableName='OCRD', FieldID=0)` (PDF s.26-27, s.87).

### Login va sessiya

```http
POST /b1s/v2/Login
{"CompanyDB":"TEST","UserName":"manager","Password":"..."}

HTTP/1.1 200 OK
Set-Cookie: B1SESSION=PTRzIjYK-weN6-1Lx1-ZG0J-3ARxfjcU0Shy;HttpOnly;
Set-Cookie: ROUTEID=.node1; path=/b1s

{
  "@odata.context": ".../b1s/v2/$metadata#B1Sessions/$entity",
  "SessionId": "PTRzIjYK-weN6-1Lx1-ZG0J-3ARxfjcU0Shy",
  "Version": "1000110",
  "SessionTimeout": 30
}
```

Manba: PDF s.16 (v1 namunasi), `@odata.context` EDMX va s.162 namunasidan xulosa.

- `B1SESSION` majburiy, `ROUTEID` ixtiyoriy (PDF s.17).
- `POST /Logout` javobi: 204, body'siz (PDF s.16).
- Sessiyasiz yoki muddati o'tgan so'rov:

```http
HTTP/1.1 401 Unauthorized
{"error":{"code":"301","message":"Invalid session or session already timeout."}}
```

Kod va matn PDF s.17 dan, tashqi ko'rinish v2 formatida (3-bo'limdagi 2-ziddiyat).

### CRUD statuslari

| Amal | Muvaffaqiyat | Manba |
|---|---|---|
| `POST` | 201, body'da obyekt, `Location` sarlavhasi | PDF s.24, s.161 |
| `GET` | 200 | PDF s.26 |
| `PATCH` | 204, body'siz. Berilmagan maydon o'zgarmaydi | PDF s.27 |
| `DELETE` | 204, body'siz | PDF s.27 |
| Action (`Close`, `Cancel`) | `POST /Orders(22)/Close` | PDF s.29 |

Faqat o'qiladigan maydon `PATCH`da jim e'tiborsiz qoldiriladi (PDF s.27).

### Ro'yxat va sahifalash

- `{"value":[...],"@odata.nextLink":"..."}`; standart sahifa 20 ta (PDF s.35).
- `Prefer: odata.maxpagesize=50` so'rovda, `Preference-Applied: odata.maxpagesize=50` javobda. `0` sahifalashni o'chiradi (PDF s.35).
- `$skip` har doim `$top`dan oldin qo'llanadi (PDF s.32). `$select=*` ruxsat etilgan (PDF s.33).

### `$filter`

PDF s.32: `startswith`, `endswith`, `contains`, `substringof`; `and`, `or`, `not`, `le`, `lt`, `ge`, `gt`, `eq`, `ne`; qavslar. Arifmetika qo'llab-quvvatlanmaydi (PDF s.225).

- Enum: nom ham, qiymat ham qabul qilinadi: `CardType eq 'C'` va `CardType eq 'cCustomer'` (PDF s.33-34). `POST`da ham shunday (PDF s.24).
- Sana: `'2014-04-23'`, `'20140423'`, `datetime'2014-04-23'`, `'2014-04-23T12:21:21'` (PDF s.34).

### Xatolar

| Holat | HTTP | Kod | Matn | Manba |
|---|---|---|---|---|
| Sessiya yo'q | 401 | 301 | `Invalid session or session already timeout.` | PDF s.17 |
| Dublikat BP | 400 | -10 | `1320000140 - Business partner code 'c1' already assigned to a business partner; enter a unique business partner code` | PDF s.25 |
| Hujjatni o'chirish | 400 | -5006 | `The requested action is not supported for this object.` | PDF s.28 |
| Topilmadi | 404 | -2028 | Matni kontekstga bog'liq, s.80 da xususiyat uchun namuna | PDF s.80 |
| Boshqa foydalanuvchi o'zgartirgan | 412 | -2039 | `Another user or another operation modified data; ...` | PDF s.162 |
| SQL sintaksisi, DML, `select *`, noma'lum funksiya | 400 | 701 | `Invalid SQL syntax:...` | PDF s.154-157 |
| Jadvalga ruxsat yo'q | 400 | 702 | `Table 'CRD2' not accessible` | PDF s.154 |
| Ustunga ruxsat yo'q | 400 | 703 | `Column 'Algo' from table 'CINF' not accessible` | PDF s.147-148 |
| Parametr xatosi | 400 | 704 | `Parameter error.` | PDF s.160 |
| SQLQueries'ga ruxsat yo'q | 403 | -6006 | `You are not permitted to perform this action` | PDF s.158 |

Quyidagi xatolar uchun SAP hujjatida ham, ochiq qidiruvda ham aniq namuna topilmadi. **Bular mock tanlovi va tekshirilmagan.** Ular `mock-sl/src/MockSl.Domain/Errors/SapErrors.cs`da turadi, farq chiqsa faqat shu fayl tuzatiladi.

| Holat | HTTP | Kod | Mock matni | Asos |
|---|---|---|---|---|
| Noto'g'ri login | 401 | -1 | `Login failed` | Asos yo'q |
| Obyekt topilmadi | 404 | -2028 | `No matching records found (ODBC -2028)` | Kod va status PDF s.80, matn tanlov |
| Majburiy maydon yo'q | 400 | -10 | `Property 'CardCode' of 'BusinessPartner' is required` | -10 biznes tekshiruvi kodi (PDF s.25), matn tanlov |
| Noma'lum maydon | 400 | -1000 | `Property 'Foo' of 'BusinessPartner' is invalid` | -1000 noto'g'ri so'rov uchun ishlatiladi (PDF s.47, s.77), matn tanlov |
| Juda uzun qiymat | 400 | -1000 | `Value too long in property 'CardCode' of 'BusinessPartner'` | Xuddi shunday |
| Noto'g'ri qiymat yoki enum | 400 | -1000 | `Invalid value 'x' for property 'CardType'` | Xuddi shunday |
| `$filter` sintaksisi | 400 | -1000 | `Invalid $filter: ...` | Xuddi shunday |
| Noto'g'ri `$top`, `$skip`, `$orderby` | 400 | -1000 | `Invalid value 'x' for query option '$top'` | Xuddi shunday |
| Qurilmagan so'rov opsiyasi | 400 | -1000 | `Query option '$apply' is not supported` | Xuddi shunday |
| Body JSON obyekt emas | 400 | -1000 | `The request body is not a valid JSON object` | Xuddi shunday |
| Noma'lum obyekt yoki URL | 404 | -1000 | `Unrecognized resource path.` | Xuddi shunday |

Shuning uchun backend va Postman'ning `real-sap` testlari bu xatolarda matn va kodga emas, statusning 4xx ekaniga va `error.code`, `error.message` borligiga qaraydi.

Biznes qoidalar xatolari ham mock tanlovi: HTTP 400, kod -10, matni `SapErrors.cs`da. Faqat -10 kodi va `Item code 'x' already exists` matni PDF'da bor (s.25, s.79).

| Holat | Mock matni |
|---|---|
| Noma'lum hamkor, mol, xodim, hisob, sotuv | `No matching record for 'x' in property 'CardCode'` |
| Hamkor turi noto'g'ri | `Business partner 'x' cannot be used for this document type` |
| Hujjatda qator yo'q | `The document must contain at least one line` |
| Miqdor yoki summa musbat emas | `Property 'Quantity' must be greater than zero` |
| Qoldiq yetmaydi | `Quantity falls into negative inventory for item 'x' in warehouse '01'` |
| Oyma-oy to'lovlar yig'indisi mos emas | `The installments must add up to the document total` |
| Allaqachon bekor qilingan yoki yopilgan | `The document is already cancelled`, `The document is already closed` |
| To'lovi bor hujjatni bekor qilish | `Cancel the payments of this document first` |
| Yopilgan hujjatga to'lov | `Document 5 is closed or cancelled and cannot be paid` |
| Ochiq summadan ortiq to'lov | `The sum applied exceeds the open amount of document 5` |
| Boshqa hamkorning hujjati | `Document 5 belongs to another business partner` |
| Ishlatilayotgan obyektni o'chirish | `The object is used by other documents and cannot be removed` |

### UDF

```http
POST /b1s/v2/UserFieldsMD
{"Name":"Region","Type":"db_Alpha","Size":50,"Description":"Hudud","SubType":"st_None","TableName":"OCRD"}
```

- `Name`da `U_` prefiksi yo'q; ustun va API maydoni `U_Region` bo'ladi (PDF s.86-87, s.89).
- Kalit kompozit: `TableName`, `FieldID` (EDMX, PDF s.87).
- Javobda `FieldID`, `EditSize`, `Mandatory`, `ValidValuesMD` va boshqalar (PDF s.87).
- `FieldID` har jadval uchun alohida, 0 dan boshlanadi (PDF s.87-88).
- `PATCH` faqat `EditSize` va `Description`ni o'zgartiradi, `Type`ni o'zgartirib bo'lmaydi (PDF s.88). `DELETE` ustunni olib tashlaydi (PDF s.89).
- UDF oddiy maydondek yoziladi va filtrlanadi: `POST BusinessPartners` body'sida, `$filter=startswith(U_BPSpecRemarks,'First')` (PDF s.89-90).
- Real SAP UDF'ni bog'liq jadvallarga ham yozadi: `OCRD` bilan `ACRD`, shuning uchun `$filter=Name eq 'u1'` ikki qator qaytaradi (PDF s.87-88). Mock faqat so'ralgan jadvalga yozadi.
- Mock'dagi tiplar: `db_Alpha` -> `VARCHAR(EditSize)`, `db_Memo` -> `TEXT`, `db_Numeric` -> `INT`, `db_Date` -> `DATE`, `db_Float` -> `NUMERIC(19,6)`. SDK `UserFieldsMD.SubType` sahifasi `Type` va `SubType` juftliklarini beradi, PostgreSQL tiplari mock tanlovi.
- UDF faqat ro'yxatdagi jadvallarga qo'shiladi: `OCRD`, `OHEM`, `OITM`, `OACT`, `OINV`, `OPCH`, `ORCT`, `OVPM`. Boshqa jadval va noto'g'ri nom SQL'gacha yetmasdan rad etiladi, chunki DDL'da parametr ishlatib bo'lmaydi.

### `$expand`

Hujjat so'ragan shakl PDF s.50 da tasdiqlangan:

```http
GET /b1s/v2/ServiceCalls(1)?$expand=BusinessPartner($select=ContactPerson,CardCode)&$select=Subject
```

Javob shakli: bog'lanish nomi bilan ichma-ich obyekt, ro'yxatda har qatorda alohida (PDF s.50-51).

| Yozuv | Ma'nosi | Manba |
|---|---|---|
| `$expand=BusinessPartner` | Hamkorning barcha maydonlari | PDF s.83 |
| `$expand=BusinessPartner($select=CardName,Phone1)` | Faqat shu maydonlar | PDF s.50 (FP 2105 dan) |
| `$select=*,BusinessPartner&$expand=BusinessPartner` | Hujjat to'liq va hamkor | PDF s.83 |
| `$select=CardCode,BusinessPartner/CardName&$expand=BusinessPartner` | Yo'l bilan tanlash | PDF s.50, s.83 |
| `$select=CardCode,BusinessPartner/CardName` | `$expand` yo'q: yo'l e'tiborsiz qoldiriladi | PDF s.83 |

Qaysi obyektda qanday bog'lanish bor:

| Obyekt | Bog'lanish | Kalit | Mock | Manba |
|---|---|---|---|---|
| `Invoices`, `PurchaseInvoices` | `BusinessPartner` | `CardCode` | Bor | EDMX: `Document` turida bor, PDF s.83 `Orders` bilan |
| `IncomingPayments` | `BusinessPartner` | `CardCode` | Bor | Hujjat talabi. EDMX: `Payment`da yo'q (1-ziddiyat) |
| `VendorPayments` | `BusinessPartner` | | Yo'q | EDMX: `Payment`da yo'q, hujjat so'ramaydi |
| `Items`, `EmployeesInfo` | `BusinessPartner` | | Yo'q | EDMX'da bor, lekin hujjat so'ramaydi |

`DocumentLines` `$expand` emas: u obyektning oddiy maydoni (hujjat, PDF s.25).

Ko'p qatorli `$expand` sekin bo'lishi mumkin (PDF s.83). Mock butun sahifa uchun bitta `WHERE "CardCode" IN (...)` so'rovi yuboradi.

### SQLQueries

- Maydonlar: `SqlCode` (kalit), `SqlName`, `SqlText`, `ParamList`, `CreateDate`, `UpdateDate` (PDF s.140, EDMX).
- `POST` javobida `SqlText` normallashtirilgan, `ParamList` so'rovdan ajratib olingan (PDF s.141, s.152).
- `List` GET va POST orqali ishlaydi (PDF s.143, s.152):
  - `POST SQLQueries('x')/List` + `{"ParamList":"a='v'&b=1"}`
  - `GET SQLQueries('x')/List?a='v'&b=1`
- Javob: `{"SqlText":"...","value":[...],"@odata.nextLink":"..."}` (PDF s.144-145, s.152-153).
- Qo'llab-quvvatlanadi (PDF s.148-149): Select/From/Where, Alias, And/Or/Not, Between, Order By, Group By + Having, Is Null, Like, Top, Union, In, Exists, Inner/Left/Right/Full Join; `Sum`, `Avg`, `Max`, `Min`, `Count`, `Distinct`, `IfNull`, `IsNull`, `Lower`, `Upper`, `Left`, `Right`.
- `select *` faqat ichki so'rovda mumkin (PDF s.156-157).
- Identifikator harfi normallashtiriladi; alias'lar saqlanadi; `IsNull` va `IfNull` almashtiriladi (PDF s.150-151).
- Superuser hammasini qila oladi. Oddiy foydalanuvchiga o'qish mumkin, `List`, yaratish, o'zgartirish, o'chirish uchun ruxsat kerak (PDF s.157-159).

Ruxsat etilgan jadvallardan loyihaga tegishlilari (PDF s.146): `OCRD`, `OINV`, `INV1`, `INV6`, `OPCH`, `PCH1`, `ORCT`, `RCT2`, `OVPM`, `VPM2`, `OITM`, `OITW`, `OACP`. To'liq ro'yxat real serverdagi `b1s_sqltable.conf`da.

Mock'da shu ro'yxat `SqlQueryPolicy.AllowedTables`da.

| Holat | HTTP | Kod | Matn | Manba |
|---|---|---|---|---|
| Faqat `SELECT` | 400 | 701 | `Invalid SQL syntax:..., line 1, character position 0, mismatched input 'update' expecting {SELECT, '('}` | PDF s.157 |
| Grammatika xatosi | 400 | 701 | `..., character position 47, Incorrect syntax near 'dsc'.` | PDF s.154 |
| Noma'lum funksiya | 400 | 701 | `..., character position 19, Cannot support this function or expression: 'length'` | PDF s.155 |
| Bir xil alias | 400 | 701 | `..., character position 55, Multiple name for column or column alias 'x', please specify it with a different alias.` | PDF s.155 |
| Yuqori darajada `*` | 400 | 701 | `..., character position 7, Cannot support asterisk(*) in select list.` | PDF s.156 |
| Hisoblangan ustunda alias yo'q | 400 | 701 | `A computed column needs an alias.` | Holat PDF s.156 da, matn mock tanlovi |
| Izoh, bir nechta buyruq, `::`, `LIMIT` | 400 | 701 | Mock tanlovi | Qaror: real xulqi tekshirilmagan |
| PostgreSQL rad etdi (`GROUP BY`, ikki ma'noli ustun) | 400 | 701 | `Invalid SQL syntax: <PostgreSQL matni>` | Kod PDF'dan, matn real SAP'dan farq qiladi |
| Ruxsat etilmagan jadval | 400 | 702 | `Table 'CRD2' not accessible` | PDF s.154 |
| Noma'lum ustun | 400 | 703 | `Column 'Algo' from table 'CINF' not accessible` | PDF s.148: u yerda yopilgan ustun haqida. Mock noma'lum ustunga ham ishlatadi |
| Parametr xatosi | 400 | 704 | `Parameter error.` | PDF s.160 |
| Oddiy foydalanuvchi | 403 | -6006 | `You are not permitted to perform this action` | PDF s.158 |

Ikki bosqichli tekshiruv: normallashtiruvchi saqlashda rad etadi, u ko'rmagan xatoni (`GROUP BY` kabi) PostgreSQL `List` paytida rad etadi. Saqlangan matn har `List`da qayta tekshiriladi: o'qilayotgan UDF o'chirilgan bo'lsa, 703.

### Query Manager va `[%0]`

Query Manager Service Layer emas. U SAP B1 desktop mijozining ichidagi vosita (Tools -> Queries -> Query Manager): saqlangan SQL so'rovlarni ro'yxatlaydi va ishga tushiradi. `[%0]`, `[%1]` ishga tushirishda foydalanuvchidan qiymat so'raydigan parametrlar. Bu sintaksis mock'da ishlamaydi, uni faqat mentor real SAP'da tekshiradi.

Manbalar: [SAP Help](https://help.sap.com/saphelp_sbo91/helpdata/en/45/0942f165e16c32e10000000a114a6b/content.htm), [Query Generator va Query Manager farqi](https://sap-b1-blog.com/en/glossar/query-generator-vs-query-manager/).

HANA'da yozilishi bo'yicha manbalar bir xil emas. Quyidagilarning faqat qidiruv qisqacha mazmuni ko'rildi, sahifalar ochilmadi (tarmoq uzilishi):

| Manba | Nima deydi |
|---|---|
| [SAP Community, 2025](https://community.sap.com/t5/technology-q-a/query-error-when-adding-a-variable-0/qaa-p/14226421/highlight/true) | Tepada jadval va maydonni ko'rsatuvchi izoh bilan `'[%0]'` ishlagan |
| [SBO HANA query with user parameters, 2022](https://answers.sap.com/t5/enterprise-resource-planning-q-a/sbo-hana-query-with-user-parameters/qaq-p/12574552) | SQL Server'dan HANA'ga o'tganda `'[%0]'`, `"[%0]"` va `[%0]` uchalasi ham ishlamagan, yechim yozilmagan |
| [Date parameter with a sub-query, 2017](https://community.sap.com/t5/enterprise-resource-planning-q-a/unable-to-use-date-parameter-in-a-query-with-a-sub-query-query-generator/qaq-p/545072) | Ichki so'rovda parametr ishlamasligi mumkin. Bizning fayllarda ichki so'rov yo'q |

### SQL vazifalari

`sql/` papkasi mock bazasida, do'kon ma'lumoti ustida ishlaydi. Ustun nomlari SDK'dan (4-bo'lim). Qoidalar hujjatdan: nomlar qo'shtirnoqda, sanalar statik, bekor qilinganlar chiqarilmaydi, bazadagi qiymat API'dagidan farq qiladi.

| Mavzu | Qaror | Manba |
|---|---|---|
| Bekor qilinganlar | `"CANCELED" = 'N'`, to'lovda `"Canceled" = 'N'`. `'C'` (bekor qilish hujjati) ham chiqmaydi | Hujjat, SDK |
| Shu oy | `>= '2026-11-01' AND < '2026-12-01'`: vaqt qismi bo'lsa ham oxirgi kun yo'qolmaydi | Qaror |
| Eng qimmat mol | `LIMIT 1`, tenglikda eng oldingi hujjat. Do'konda Go'shtni 85 dan ikki mijoz olgan | Hujjat, qaror |
| "Ko'p pul sarflagan" | `SUM("DocTotal")`, `PaidToDate` emas | Hujjat aniq aytmagan, talqin |

## 3. Ziddiyatlar

| # | Mavzu | Manbalar nima deydi | Qaror |
|---|---|---|---|
| 1 | `IncomingPayments` + `$expand=BusinessPartner` | Hujjat: mock'da kamida `Invoices`, `PurchaseInvoices`, `IncomingPayments` uchun. EDMX: `Payment`da `BusinessPartner` navigatsiyasi yo'q, `Document`da bor | Hujjat so'ragandek quriladi. Real SAP'da xato berishi kutiladi, mentor tekshiradi. Backend bunga tayanmaydi |
| 2 | v2 xato formati | PDF v1 namunalari: `{"code":-10,"message":{"lang":"en-us","value":"..."}}`. PDF s.162 (v2): `{"code":"-2039","message":"..."}`. SAP KBA'larda ikkalasi ham uchraydi | Mock: v2 namunasi. Backend parser'i ikkala shaklni qabul qiladi |
| 3 | Sessiya tugashi kodi | PDF s.17: 401 / 301. Apideck: -5002 | PDF. Backend faqat HTTP 401 ga qaraydi |
| 4 | Keyingi sahifa nomi | PDF s.35: v4 uchun `@odata.nextLink`. SAP Learning (v2 namunasi): `odata.nextLink` | PDF va hujjat: `@odata.nextLink`. Backend bunga tayanmaydi |
| 5 | -2028 ma'nosi | PDF s.80: topilmadi, 404. Apideck: locked object | PDF |
| 6 | Yangi UDF'ning ko'rinishi | Hujjat: yaratilgach javoblarda chiqadi. PDF s.225: Service Layer qayta ishga tushmaguncha ko'rinmaydi | Mock: darhol, hujjatdagidek. Real SAP'da restart kerak bo'lishi mumkin |
| 7 | `ORCT.DocTotal` | SDK: ustun bor. EDMX: `Payment`da `DocTotal` yo'q | Bazada hisoblanadi, API javobida chiqmaydi |
| 8 | Chiqim to'lov jadvali | Hujjat bu xatoni o'zi qayd etgan: `OVMP` emas, `OVPM`. SDK tasdiqlaydi | `OVPM` |
| 9 | `OACP` | Hujjat: "Standart hisoblar". SDK sarlavhasi: Periods Category. EDMX: alohida EntitySet yo'q, `CompanyService_GetPeriod(s)` + `PeriodCategory` | Jadval hujjatdagidek, o'qish shu amallar orqali |
| 10 | `Payment.DocType` | EDMX'da `DocType` ham, xato yozilgan `DocTypte` ham bor. SDK: ustun `DocType` | Mock javobda ikkalasini ham ko'rsatadi, kirishda e'tiborsiz qoldiradi: tur obyektdan aniqlanadi (`IncomingPayments` = mijoz, `VendorPayments` = ta'minotchi) |
| 11 | Backend login'i qayerdan keladi | Hujjatning qabul shartida ".env'dagi SL_BASE_URL va login ma'lumotlari" deyilgan. Vazifada esa `POST /api/login` "SAP login va paroli bilan" | Backend login'ni API orqali oladi, `.env`da parol saqlamaydi. `.env`da `SL_BASE_URL` va `SL_COMPANY_DB`. Do'kon skripti uchun esa login `.env`da bo'ladi. Mentor bilan aniqlashtirish mumkin |
| 12 | `ChartOfAccounts.CashAccount` ustuni | SDK xususiyat sahifasi: `CashBox`. SDK jadval sahifasi: `Finanse` = "Cash Account", `CashBox` = "Capital Account" | `Finanse`: jadval sahifasi aniqroq ma'noni beradi. Real SAP'da tekshirilsin |
| 13 | `PaymentInvoices.LineNum` ustuni | SDK xususiyat sahifasi: `DocLine`. Jadvalda `InvoiceId` = "Sequence No." (kalit), `DocLine` = "Row Key" | Mock: `InvoiceId`, chunki kalit shu. `DocLine` qurilmagan |
| 14 | `UnitPrice` ustuni | SDK xususiyat sahifasi: `UnitPrice`. Jadvalda: `PriceBefDi` = "Unit Price", `Price` = "Price after Discount" | Chegirma qurilmagan, shuning uchun ikkala nom ham `Price` ustunida. Hujjat SQL vazifalarida `Price` ishlatadi |
| 15 | Xaridda oyma-oy to'lov | Hujjat: `OPCH`, `PCH1`. Real SAP'da `PCH6` ham bor (PDF s.146) | Hujjatdagidek: `PCH6` qurilmagan, `DocumentInstallments` xaridda rad etiladi |
| 16 | UDF hajmi | PDF s.87 namunasi `Size` yuboradi. SDK: `Size`ga qiymat bermang, u `EditSize`dan aniqlanadi | Mock ikkalasini qabul qiladi, `EditSize` ustun. Ikkala ustunga bir xil qiymat yoziladi |
| 17 | `UserFieldsMD.Mandatory` ustuni | SDK xususiyat sahifasi: `Sys`. SDK jadval sahifasi: `NotNull` = "Required Entry Field", `Sys` = "System Defined?" | `NotNull`: jadval sahifasi aniqroq ma'noni beradi. Mock'da faqat o'qiladi |
| 18 | `SQLQueries` nomlarining harfi | PDF s.148: nomlar bazadagidek yozilsin. PDF s.150: Service Layer harfni o'zi moslaydi. Hujjat: "Service Layer o'zi moslaydi" | s.150 va hujjatdagidek moslanadi. Vazifa matnida harflar baribir aniq, shuning uchun real'da ikkala holatda ham ishlashi kerak |

## 4. Maydonlar mosligi

Service Layer nomlari EDMX'dan, ustun nomlari SDK jadval sahifalaridan olingan. Ular orasidagi moslik SDK xususiyat sahifalaridan tekshirildi (`SDKHelp/SAPbobsCOM~Documents~DocumentsOwner.html` kabi, har birida "Field name" bor).

- **SDK tasdiqlagan:** hujjat: `DocEntry`, `DocNum`, `DocDate`, `DocDueDate`, `DocTotal`, `DocumentStatus`, `DocumentsOwner`, `SalesPersonCode`, `NumberOfInstallments`, `PaymentGroupCode`, `Comments`. Qator: `ItemDescription`, `WarehouseCode`, `LineStatus`, `LineTotal`, `Price`, `TaxCode`, `VatGroup`. Oyma-oy: `Total`, `Percentage`, `DueDate`. To'lov: `CashAccount`, `CashSum`, `Remarks`, `DocType`. To'lov qatori: `DocEntry`, `SumApplied`, `InvoiceType`, `InstallmentId`. Hamkor: `CurrentAccountBalance`, `EmailAddress`. Hisob: `Code`, `Name`, `Balance`, `ActiveAccount`, `FatherAccountKey`, `AccountLevel`, `AccountType`, `FormatCode`. Xodim: `EmployeeID`, `FirstName`, `LastName`, `JobTitle`. `OACP`: `DebitorsFollowUpAccount`, `AccountforCashReceipt`, `DefaultSaleAccount`, `ExpenseAccountDefault`.
- **SDK boshqacha deydi:** `CashAccount`, `LineNum`, `UnitPrice`: 3-bo'lim, 12-14-ziddiyatlar.
- **Sahifada ustun yozilmagan, nom bo'yicha xulosa:** `Cancelled`, `CancelStatus`, `QuantityOnStock`, `InventoryItem`, `SalesItem`, `PurchaseItem`, `WarehouseCode`/`InStock` (`OITW`), `Active`.

Real tizimda hammasini bitta so'rov bilan tasdiqlash mumkin: `GET /b1s/v2/$metadata?scope=entityset&annotation=labelWithTable` (PDF s.19-22, FP 2608 dan).

### BusinessPartners: `OCRD`, kalit `CardCode`

| Service Layer | Ustun | Tip | Izoh |
|---|---|---|---|
| `CardCode` | `CardCode` | nVarChar(15) | Kalit |
| `CardName` | `CardName` | nVarChar(200) | |
| `CardType` | `CardType` | VarChar(1) | `cCustomer`=C, `cSupplier`=S, `cLid`=L |
| `Phone1` | `Phone1` | nVarChar(50) | |
| `Cellular` | `Cellular` | nVarChar(50) | |
| `EmailAddress` | `E_Mail` | nVarChar(100) | |
| `Address` | `Address` | nVarChar(100) | |
| `City` | `City` | nVarChar(100) | |
| `CurrentAccountBalance` | `Balance` | Num(19,6) | Faqat o'qiladi |

### EmployeesInfo: `OHEM`, kalit `EmployeeID`

| Service Layer | Ustun | Izoh |
|---|---|---|
| `EmployeeID` | `empID` | Int, server beradi |
| `FirstName` | `firstName` | nVarChar(50) |
| `LastName` | `lastName` | nVarChar(50) |
| `JobTitle` | `jobTitle` | nVarChar(20) |
| `Active` | `Active` | `tYES`=Y, `tNO`=N |

### Items: `OITM`, `OITW`, kalit `ItemCode`

| Service Layer | Ustun | Izoh |
|---|---|---|
| `ItemCode` | `ItemCode` | nVarChar(50) |
| `ItemName` | `ItemName` | nVarChar(200) |
| `InventoryItem` | `InvntItem` | `tYES`/`tNO` |
| `SalesItem` | `SellItem` | `tYES`/`tNO` |
| `PurchaseItem` | `PrchseItem` | `tYES`/`tNO` |
| `QuantityOnStock` | `OnHand` | Faqat o'qiladi |
| `ItemWarehouseInfoCollection[].WarehouseCode` | `OITW.WhsCode` | nVarChar(8) |
| `ItemWarehouseInfoCollection[].InStock` | `OITW.OnHand` | Faqat o'qiladi |

### Invoices: `OINV`, `INV1`, `INV6`; PurchaseInvoices: `OPCH`, `PCH1`. Kalit `DocEntry`

| Service Layer | Ustun | Izoh |
|---|---|---|
| `DocEntry` | `DocEntry` | Ichki kalit, server beradi |
| `DocNum` | `DocNum` | Foydalanuvchiga ko'rinadigan raqam |
| `DocType` | `DocType` | `dDocument_Items`=I, `dDocument_Service`=S |
| `DocDate` | `DocDate` | Date |
| `DocDueDate` | `DocDueDate` | Date |
| `CardCode` | `CardCode` | `OCRD` |
| `CardName` | `CardName` | |
| `DocTotal` | `DocTotal` | Qatorlardan hisoblanadi |
| `PaidToDate` | `PaidToDate` | To'lovlardan yig'iladi |
| `DocumentStatus` | `DocStatus` | `bost_Open`=O, `bost_Close`=C |
| `Cancelled` | `CANCELED` | `tYES`=Y, `tNO`=N |
| `CancelStatus` | `CANCELED` | `csYes`=Y, `csNo`=N, `csCancellation`=C |
| `DocumentsOwner` | `OwnerCode` | `OHEM`ga bog'lanadi. Xodim shu orqali biriktiriladi |
| `SalesPersonCode` | `SlpCode` | `OSLP`ga bog'lanadi, ishlatilmaydi |
| `NumberOfInstallments` | `Installmnt` | |
| `PaymentGroupCode` | `GroupNum` | |
| `DocumentLines` | `INV1` / `PCH1` | Oddiy maydon sifatida keladi |
| `DocumentInstallments` | `INV6` | |

Qatorlar:

| Service Layer | Ustun |
|---|---|
| `LineNum` | `LineNum` |
| `ItemCode` | `ItemCode` |
| `ItemDescription` | `Dscription` |
| `Quantity` | `Quantity` |
| `Price`, `UnitPrice` | `Price` (chegirma qurilmaydi) |
| `LineTotal` | `LineTotal` |
| `WarehouseCode` | `WhsCode` |
| `LineStatus` | `LineStatus` |

Oyma-oy to'lovlar:

| Service Layer | Ustun |
|---|---|
| `InstallmentId` | `InstlmntID` |
| `DueDate` | `DueDate` |
| `Total` | `InsTotal` |
| API'da yo'q | `PaidToDate`, `Status` (faqat bazada) |

### IncomingPayments: `ORCT`, `RCT2`; VendorPayments: `OVPM`, `VPM2`. Kalit `DocEntry`

| Service Layer | Ustun | Izoh |
|---|---|---|
| `DocEntry` | `DocEntry` | |
| `DocNum` | `DocNum` | |
| `DocType`, `DocTypte` | `DocType` | `rCustomer`=C, `rAccount`=A, `rSupplier`=S |
| `DocDate` | `DocDate` | |
| `CardCode`, `CardName` | `CardCode`, `CardName` | |
| `CashAccount` | `CashAcct` | `OACT` |
| `CashSum` | `CashSum` | |
| `Cancelled` | `Canceled` | `tYES`=Y, `tNO`=N |
| API'da yo'q | `DocTotal` | Bazada hisoblanadi |

`PaymentInvoices`, **e'tibor bering: nomlar aldamchi.**

| Service Layer | Ustun | Izoh |
|---|---|---|
| to'lov kaliti | `RCT2.DocNum` | `ORCT.DocEntry`ni ko'rsatadi |
| `LineNum` | `InvoiceId` | Tartib raqami |
| `DocEntry` | `RCT2.DocEntry` | **Sotuvning** kaliti |
| `SumApplied` | `SumApplied` | |
| `InvoiceType` | `InvType` | `it_Invoice`=13, `it_PurchaseInvoice`=18 |
| `InstallmentId` | `InstId` | |

### ChartOfAccounts: `OACT`, kalit `Code`

| Service Layer | Ustun | Izoh |
|---|---|---|
| `Code` | `AcctCode` | nVarChar(15) |
| `Name` | `AcctName` | nVarChar(200) |
| `Balance` | `CurrTotal` | Faqat o'qiladi |
| `CashAccount` | `Finanse` | `tYES`=Y, `tNO`=N |
| `ActiveAccount` | `Postable` | `tYES`=Y (aktiv), `tNO`=N (sarlavha) |
| `FatherAccountKey` | `FatherNum` | |
| `AccountLevel` | `Levels` | |
| `AccountType` | `ActType` | `at_Revenues`=I, `at_Expenses`=E, `at_Other`=N |

Cookbook namunasida `POST` body'si `FatherAccountKey`, `FormatCode`, `Name` bilan, `Code`siz. Skript javobdan qaytgan `Code`ni ishlatadi.

### UserFieldsMD: `CUFD`, kalit `TableName`, `FieldID`

EDMX: `UserFieldMD`. Ustun nomlari SDK xususiyat sahifalarida yozilgan, faqat ikkitasi xulosa.

| Service Layer | Ustun | Mock'da | Manba |
|---|---|---|---|
| `Name` | `AliasID` | Majburiy, keyin o'zgarmaydi | SDK. `U_`siz |
| `Type` | `TypeID` | Majburiy, keyin o'zgarmaydi | SDK, PDF s.88 |
| `Size` | `SizeID` | `EditSize` bilan bir xil | SDK |
| `Description` | `Descr` | O'zgaradi | Jadval sahifasi, nom bo'yicha xulosa |
| `SubType` | `EditType` | Keyin o'zgarmaydi | Jadval sahifasi ("Edit Type"), nom bo'yicha xulosa |
| `LinkedTable` | `RTable` | Faqat o'qiladi | SDK |
| `DefaultValue` | `Dflt` | Faqat o'qiladi | SDK |
| `TableName` | `TableID` | Kalit, majburiy | SDK |
| `FieldID` | `FieldID` | Kalit, server beradi | SDK |
| `EditSize` | `EditSize` | Matn ustunida faqat kattalashadi | SDK, PDF s.88 |
| `Mandatory` | `NotNull` | Faqat o'qiladi | 17-ziddiyat |
| `LinkedUDO` | `RelUDO` | Faqat o'qiladi | SDK |

`SubType` qiymati: EDMX'dagi son aslida belgi kodi (`st_Phone=35` = `#`, `st_Sum=83` = `S`). `TypeID` harflari (`A`, `M`, `N`, `D`, `B`) mock tanlovi: SDK'dagi `BoFieldTypes` sahifasi kirishni so'radi, EDMX esa faqat tartib raqamini beradi (23-savol).

### SQLQueries: `OSQL`, kalit `SqlCode`

Service Layer va ustun nomlari bir xil (EDMX, SDK).

| Service Layer | Ustun | Mock'da |
|---|---|---|
| `SqlCode` | `SqlCode` | Kalit, majburiy |
| `SqlName` | `SqlName` | O'zgaradi |
| `SqlText` | `SqlText` | Majburiy, normallashtirilib saqlanadi (PDF s.141) |
| `ParamList` | `ParamList` | Faqat o'qiladi, matndan olinadi (PDF s.152) |
| `CreateDate` | `CreateDate` | Faqat o'qiladi, server qo'yadi |
| `UpdateDate` | `UpdateDate` | Faqat o'qiladi, server qo'yadi |

SDK'da yana bor, mock'da qurilmagan: `ParamDetai`, `InternalS`, `LogInstanc`, `ObjType`, `DataVers`.

### Standart hisoblar: `OACP`, kalit `AbsoluteEntry`

EDMX'da alohida EntitySet yo'q, faqat ikkita global action bor. Mock faqat o'qiydi. `PeriodCategory`da 128 ta maydon bor, hujjat uchun kerakli to'rttasi quriladi.

| Service Layer | Ustun | Ma'nosi (SDK) |
|---|---|---|
| `AbsoluteEntry` | `AbsEntry` | Kalit |
| `PeriodCategoryProperty` | `PeriodCat` | Davr |
| `BeginningofFinancialYear` | `FinancYear` | Moliya yili boshi |
| `DebitorsFollowUpAccount` | `LinkAct_1` | Debitorlar |
| `AccountforCashReceipt` | `LinkAct_3` | Kassa |
| `DefaultSaleAccount` | `DfltIncom` | Daromad |
| `ExpenseAccountDefault` | `DfltExpn` | Xarajat |

Mock qarori: to'lovda `CashAccount` berilmasa, `LinkAct_3` ishlatiladi. Real SAP hisobni talab qilishi mumkin (20-savol).

## 5. Javobi topilmagan savollar

Bular real SAP'da tekshirilishi kerak.

| # | Savol | Hozirgi qaror | Xavf |
|---|---|---|---|
| 1 | v2 da sana qanday chiqadi? EDMX `Edm.DateTimeOffset`, PDF v1 namunasi `2014-04-01`, `List` namunasi `20201007` | Mock: obyektlarda `2026-11-01T00:00:00Z`, `List`da PDF namunasi | Backend sanani o'qimaydi, skript faqat yuboradi: `2026-11-01` |
| 2 | `$top` va `@odata.nextLink` birga qanday ishlaydi? `$top` standart 20 dan katta bo'lsa nima bo'ladi? | Mock OData qoidasi bo'yicha | Backend `Prefer` va `pageSize+1` ishlatgani uchun bog'liq emas |
| 3 | Noto'g'ri login uchun status, kod va matn qanday? PDF'da yo'q. Qidirildi: [SBO 2311 dagi 401 mavzusi](https://answers.sap.com/t5/crm-and-cx-questions/sbo-2311hf1-service-layer-issue-async-logins-bug-help/qaq-p/13594010) boshqa sababga oid, parol xatosi uchun namuna topilmadi | Mock: 401, -1, `Login failed` | Backend faqat statusni o'tkazadi |
| 4 | Noto'g'ri kalitda 404 matni qanday? | PDF'dagi -2028 kodi, matn tanlov | Xabar shundayligicha qaytariladi |
| 5 | Noma'lum maydon, majburiy maydon va `$filter` sintaksisi uchun kod va matn qanday? Qidirildi, Service Layer namunasi topilmadi | Mock: 2-bo'limdagi ikkinchi jadval | Xabar shundayligicha qaytariladi |
| 6 | `DocumentInstallments`ni qanday yuborish kerak? Real SAP to'lov shartlari (`OCTG`) talab qilishi mumkin | `DueDate` + `Total` | Skript real'da shu joyda to'xtashi mumkin |
| 7 | Qoldiq yetmaganda xato matni va uning sozlamaga bog'liqligi | Mock rad etadi, matn taxminiy | Skript tartibi qoldiqni manfiy qilmaydi |
| 8 | Ta'minotchi `Balance` belgisi (xaridda manfiymi?) | Mock kamaytiradi | Qabul sharti faqat mijozlar uchun |
| 9 | `[%0]` HANA Query Manager'da qanday yoziladi: qo'shtirnoqsizmi, `'[%0]'`mi, tepada izoh kerakmi? Manbalar bir xil emas (2-bo'lim, Query Manager) | Hujjatdagidek `[%0]`, `[%1]`. Fayl izohida muqobil yozilgan | Mentor tekshiradi. Mantiq mock'da sana bilan tekshirildi |
| 10 | `SQLQueries`da `LIMIT` qabul qilinadimi? PDF ro'yxatida faqat `Top` bor | Mock `Top`ni qabul qiladi, `LIMIT`ni 701 bilan rad etadi | Vazifadagi `SqlText`da ikkalasi ham yo'q |
| 11 | `b1s_sqltable.conf`dagi to'liq ruxsat ro'yxati | PDF ro'yxati | Vazifa faqat `OPCH`, `PCH1` ishlatadi |
| 12 | v2 da `@odata.nextLink` qanday ko'rinishda: `Orders?$skip=20` yoki `/b1s/v2/Orders?$skip=20`? PDF'da ikkalasi ham bor (s.35 v1 da to'liq yo'l, s.143 da nisbiy) | Mock: nisbiy, OData 4 qoidasi bo'yicha | Backend `nextLink`ga tayanmaydi |
| 13 | `contains` real HANA'da harf katta-kichikligini farqlaydimi? PDF s.150: HANA case-sensitive, lekin bu identifikatorlar haqida | Mock: farqlaydi (PostgreSQL `LIKE`) | Qidiruv natijasi farq qilishi mumkin |
| 14 | `Location` sarlavhasi va `@odata.etag`: real javobda ETag bor (PDF s.161-162), mock'da yo'q | Mock ETag bermaydi | Backend `If-Match` yubormaydi |
| 15 | `PATCH`da bo'sh satr (`"Phone1":""`) maydonni tozalaydimi? | Mock bo'sh satrni saqlaydi. Backend'da null yoki yuborilmagan maydon o'zgarmaydi | Faqat maydonni tozalash holati |
| 16 | Kelajak sanasi bilan hujjat qabul qilinadimi? Skript `GROCERY_CURRENT_MONTH` oyining 25-kunigacha sana ishlatadi | Mock qabul qiladi | Real'da rad etilsa, `GROCERY_CURRENT_MONTH`ni o'tgan oyga qo'yish yetarli |
| 17 | `Invoices(1)/Cancel` real SAP'da nima qiladi? SDK `CANCELED='C'` borligini ko'rsatadi, demak alohida bekor qilish hujjati yaratiladi | Mock asl hujjatni belgilaydi, alohida hujjat yaratmaydi. `CANCELED = 'N'` filtri ikkala holatda ham to'g'ri | SQL natijasi o'zgarmaydi. Real'da ro'yxatda qo'shimcha qator ko'rinadi |
| 18 | Hujjatda `PATCH` bilan nimani o'zgartirish mumkin? `DocumentLines` qabul qilinadimi? | Mock: faqat `DocDueDate`, `DocumentsOwner`, `SalesPersonCode`, `PaymentGroupCode`, `Comments`, qolgani jim tashlanadi | Real SAP xato berishi yoki qabul qilishi mumkin |
| 19 | Real SAP'da hisob yaratish uchun nima kerak? Namuna `FatherAccountKey`, `FormatCode`, `Name` | Skript shularni yuboradi va javobdagi `Code`ni ishlatadi. `FatherAccountKey` `.env`da | Real ota hisob sarlavha hisobi bo'lishi kerak |
| 20 | `CashAccount`siz to'lov real SAP'da qabul qilinadimi? | Mock `OACP.LinkAct_3`ni oladi | Skript hisobni doim yuboradi |
| 21 | Xaridda mol yetmaganda bekor qilish, ishlatilayotgan hamkorni o'chirish: real matn qanday? | Mock rad etadi, matn tanlov | Xabar shundayligicha qaytariladi |
| 22 | To'lov bir nechta oyma-oy qismga tushsa, real SAP `RCT2`da nechta qator yozadi? | Mock: bitta qator, `INV6` esa eng erta muddatdan taqsimlanadi | SQL vazifalari `RCT2`ni bunga bog'lamaydi |
| 23 | `CUFD.TypeID` bazada qaysi harflar bilan saqlanadi? | Mock: `A`, `M`, `N`, `D`, `B`. Tasdiqlanmagan | API'da faqat nomlar chiqadi, SQL vazifalari bu ustunni ishlatmaydi |
| 24 | UDF nomiga real qoida qanday? SDK faqat uzunlikni aytadi: 50 | Mock: harf bilan boshlanadi, keyin harf, raqam, `_`. Bu qoida xavfsizlik uchun, SAP'dan emas | `Region` oddiy nom, farq chiqishi kutilmaydi |
| 25 | Matn UDF uchun hajm berilmasa nima bo'ladi? Eng katta hajm qancha? | Mock: hajm majburiy, 1 dan 254 gacha. Bu chegara manbadan emas | Vazifa 50 yuboradi |
| 26 | Matn UDF'ni kichraytirsa bo'ladimi? PDF s.88 faqat namunada kattalashtiradi | Mock rad etadi, chunki eski qiymat sig'masligi mumkin | Vazifaga kirmaydi |
| 27 | `PATCH`da `Type` yuborilsa, xato qaytadimi yoki jim e'tiborsiz qoladimi? PDF s.88: o'zgartirib bo'lmaydi | Mock jim e'tiborsiz qoldiradi, faqat o'qiladigan maydonlardek (PDF s.27) | Vazifaga kirmaydi |
| 28 | UDF o'chirilgach `FieldID` qayta beriladimi? | Mock: eng katta + 1, oxirgisi o'chirilsa, raqam takrorlanadi | Postman raqamni javobdan oladi |
| 29 | `IncomingPayments` uchun `$expand=BusinessPartner` real SAP'da ishlaydimi? | Mock: ishlaydi, hujjatdagidek (1-ziddiyat) | Postman'ning bitta so'rovi real'da tushishi mumkin |
| 30 | `sql/` so'rovlari HANA'da ishlaydimi? Faqat PostgreSQL'da ishga tushirilgan | HANA'ga mos deb tanlangan yozuv: qo'shtirnoqli nomlar, `LIMIT`, `COALESCE`, `::` va `ILIKE` yo'q | Mentor tekshiradi |
| 31 | HANA'da `"DocDate" >= '2026-11-01'` matn bilan solishtirish ishlaydimi? | Hujjat statik sanani shu shaklda so'raydi | Xato bersa, sanani `TO_DATE` bilan o'rash kerak bo'ladi. Tasdiqlanmagan |
| 32 | Real SAP'da `OINV.DocTotal`ga soliq qo'shiladi. 1-2, 11-vazifalar natijasi soliq bilan chiqadi | Mock soliq hisoblamaydi | Kutilgan natija faqat do'kon ma'lumoti va mock uchun |
| 33 | Bir nechta parametrda `ParamList` javobda qanday yoziladi? PDF faqat bitta parametrli namuna beradi | Mock: `a,b` | Backend va vazifa bunga tayanmaydi |
| 34 | `SqlText`da izoh yoki oxirida `;` real SAP'da qabul qilinadimi? | Mock rad etadi: 701 | Vazifa matnida ikkalasi ham yo'q |
| 35 | Noma'lum ustunda real kod qanday? PDF s.148 dagi 703 yopilgan ustun haqida | Mock: 703 | Postman faqat 4xx ekanini tekshiradi |
| 36 | `List`da `Prefer: odata.maxpagesize=0` nima qiladi? PDF s.144: sahifalash majburiy | Mock: umumiy qoidadek sahifalashni o'chiradi (PDF s.35) | Postman 1000 yuboradi |
| 37 | Sana parametri real'da `'2026-10-01'` shaklida o'tadimi? Hujjat shunday yozadi, PDF namunalari son va matn bilan | Hujjatdagidek. Mock'da PostgreSQL turni o'zi aniqlaydi | Vazifaning asosiy tekshiruvi. Mentor tekshiradi |
| 38 | `List` javobidagi sana v2 da ham `20201007`mi? PDF s.153 v1 namunasi | Mock: `yyyyMMdd`, PDF'dagidek (1-savolga qarang) | Backend va skript bu javobni o'qimaydi |

## 6. Mock'da ataylab qurilmagan narsalar

ETag / `If-Match` (PDF s.161-165), `Prefer: return-no-content` (s.28), `$count`, `$inlinecount` (s.32-33), `$apply`, `$crossjoin`, `$batch`, `$metadata`, xizmat hujjati (`GET /`), `PUT`, `X-HTTP-Method-Override`, UDF'ning arxiv jadvallarga yozilishi (`ACRD`). Backend va skript bularga tayanmaydi. Mock ularni `Query option ... is not supported` yoki `-5006` bilan rad etadi.

2-bosqichda ham qurilmaganlar: soliq hisobi (`TaxCode`, `VatGroup` faqat saqlanadi), chegirma, xizmat hujjatlari (`dDocument_Service`), naqddan boshqa to'lov turlari (`TransferSum` va boshqalar), `Reopen`, `CreateCancellationDocument`, `CancelbyCurrentSystemDate`, `Items(id)/Cancel`, `EmployeesInfo(id)/Cancel`, omborlar ro'yxati (`OWHS`), to'lov shartlari (`OCTG`), `PCH6`, buxgalteriya provodkalari (`OJDT`, hujjatda chiqarib tashlangan). `PeriodCategory` faqat o'qiladi.

3-bosqichda ham qurilmaganlar: `ValidValuesMD` va `LinkedSystemObject` (javobda chiqmaydi, EDMX va PDF s.87 da bor), qiymatli `LinkedTable`, `DefaultValue`, `Mandatory`, `LinkedUDO` (yuborilsa, jim e'tiborsiz qoladi), qator jadvallariga UDF (`INV1`, `RDR1`), UDT (PDF s.90), ichma-ich `$expand`, qavs ichida `$select`dan boshqa option, `Items` va `EmployeesInfo`dan bog'lanishlar. Backend'da hududni yozish yo'q: hujjat faqat ro'yxat va filtrni so'raydi.

5-bosqichda ham qurilmaganlar: to'liq SQL parser (normallashtiruvchi soddalashtirilgan, hujjat ham shuni so'raydi), `FROM` ichidagi sub query, `TOP` sub query yoki `UNION` bilan, `ColumnExcludeList` (PDF s.147), UDO va UDT so'rovlari, `ASQL` tarix jadvali (PDF s.160), General Authorizations orqali ruxsat berish (oddiy foydalanuvchi doim 403). Soddalashtirish: bitta alias ikki jadvalga berilsa, ustun harfi birinchi mos jadvaldan olinadi.

## 7. So'rov va javob namunalari

Bular mock'dan olingan haqiqiy javoblar (`docker compose`, 2026-11-06). Har birida shakl qaysi manbaga tayangani ko'rsatilgan. **Real SAP bilan solishtirilmagan:** bu mentor tekshiruvi.

Real SAP javobida farq qilishi kutiladigan narsalar:

- `BusinessPartner`da yuzlab maydon bor (EDMX), mock esa hujjat so'ragan 9 tasini qaytaradi.
- `BusinessPartners` ETag ro'yxatida (PDF s.164-165), shuning uchun real javobda `@odata.etag` va `ETag` sarlavhasi bo'ladi.
- `Version` serverga qarab boshqacha bo'ladi. Mock PDF s.16 dagi namunani qaytaradi.

### Login

Manba: PDF s.16. `@odata.context` v2 namunalaridan (s.162) xulosa.

```http
POST /b1s/v2/Login
{"CompanyDB":"TEST","UserName":"manager","Password":"1234"}

HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Set-Cookie: B1SESSION=a4aa832e-3758-4006-94f3-e113ae9037a1;HttpOnly;
Set-Cookie: ROUTEID=.node1; path=/b1s

{
  "@odata.context": "http://localhost:50000/b1s/v2/$metadata#B1Sessions/$entity",
  "SessionId": "a4aa832e-3758-4006-94f3-e113ae9037a1",
  "Version": "1000110",
  "SessionTimeout": 30
}
```

### Logout va sessiya tugagandan keyingi so'rov

Manba: PDF s.16-17.

```http
POST /b1s/v2/Logout
Cookie: B1SESSION=a4aa832e-...; ROUTEID=.node1

HTTP/1.1 204 No Content
```

```http
GET /b1s/v2/BusinessPartners
Cookie: B1SESSION=a4aa832e-...; ROUTEID=.node1

HTTP/1.1 401 Unauthorized
{"error":{"code":"301","message":"Invalid session or session already timeout."}}
```

### BusinessPartners: yaratish

Manba: PDF s.24, `Location` s.161.

```http
POST /b1s/v2/BusinessPartners
{"CardCode":"C0001","CardName":"Ali Valiyev","CardType":"cCustomer","Phone1":"+998901234567","EmailAddress":"ali@example.uz","Address":"Amir Temur 1","City":"Toshkent"}

HTTP/1.1 201 Created
Location: http://localhost:50000/b1s/v2/BusinessPartners('C0001')

{
  "@odata.context": "http://localhost:50000/b1s/v2/$metadata#BusinessPartners/$entity",
  "CardCode": "C0001",
  "CardName": "Ali Valiyev",
  "CardType": "cCustomer",
  "Phone1": "+998901234567",
  "Cellular": null,
  "EmailAddress": "ali@example.uz",
  "Address": "Amir Temur 1",
  "City": "Toshkent",
  "CurrentAccountBalance": 0
}
```

### BusinessPartners: bittasini olish

Manba: PDF s.26. Javob yaratishdagi bilan bir xil, status 200.

```http
GET /b1s/v2/BusinessPartners('C0001')
GET /b1s/v2/BusinessPartners(CardCode='C0001')
```

### BusinessPartners: ro'yxat

Manba: PDF s.32-35.

```http
GET /b1s/v2/BusinessPartners?$select=CardCode,CardName,Phone1&$filter=CardType eq 'cCustomer' and contains(CardName,'Ali')&$orderby=CardName&$top=20

HTTP/1.1 200 OK
{
  "@odata.context": "http://localhost:50000/b1s/v2/$metadata#BusinessPartners",
  "value": [
    {"CardCode": "C0001", "CardName": "Ali Valiyev", "Phone1": "+998901234567"}
  ]
}
```

Keyingi sahifa bo'lsa, javobda `"@odata.nextLink": "BusinessPartners?$select=CardCode&$skip=20"` qo'shiladi. `Prefer: odata.maxpagesize=5` yuborilsa, `Preference-Applied: odata.maxpagesize=5` qaytadi.

### BusinessPartners: o'zgartirish va o'chirish

Manba: PDF s.27.

```http
PATCH /b1s/v2/BusinessPartners('C0001')
{"City":"Samarqand"}

HTTP/1.1 204 No Content
```

```http
DELETE /b1s/v2/BusinessPartners('C0001')

HTTP/1.1 204 No Content
```

### Xatolar

```http
POST /b1s/v2/BusinessPartners
{"CardCode":"C0001","CardName":"x"}

HTTP/1.1 400 Bad Request
{"error":{"code":"-10","message":"1320000140 - Business partner code 'C0001' already assigned to a business partner; enter a unique business partner code"}}
```

```http
GET /b1s/v2/BusinessPartners('NONE')

HTTP/1.1 404 Not Found
{"error":{"code":"-2028","message":"No matching records found (ODBC -2028)"}}
```

```http
GET /b1s/v2/BusinessPartners?$filter=Foo eq 'x'

HTTP/1.1 400 Bad Request
{"error":{"code":"-1000","message":"Property 'Foo' of 'BusinessPartner' is invalid"}}
```

Birinchisining matni PDF s.25 dan. Ikkinchi va uchinchisining matni mock tanlovi (2-bo'limdagi ikkinchi jadval).

### ChartOfAccounts

Manba: cookbook namunasi (`FatherAccountKey`, `FormatCode`, `Name`). Mock `Code`ni `FormatCode`dan oladi.

```http
POST /b1s/v2/ChartOfAccounts
{"FormatCode":"1011","Name":"Do'kon kassasi","FatherAccountKey":"1000","CashAccount":"tYES","ActiveAccount":"tYES"}

HTTP/1.1 201 Created
Location: .../b1s/v2/ChartOfAccounts('1011')

{
  "Code": "1011",
  "Name": "Do'kon kassasi",
  "FormatCode": "1011",
  "Balance": 0,
  "CashAccount": "tYES",
  "ActiveAccount": "tYES",
  "FatherAccountKey": "1000",
  "AccountLevel": 2,
  "AccountType": "at_Other"
}
```

### EmployeesInfo

Manba: EDMX, cookbook namunasi. Kalit son, server beradi.

```http
POST /b1s/v2/EmployeesInfo
{"FirstName":"Vali","LastName":"Karimov","JobTitle":"Sotuvchi"}

HTTP/1.1 201 Created
Location: .../b1s/v2/EmployeesInfo(1)

{"EmployeeID":1,"FirstName":"Vali","LastName":"Karimov","JobTitle":"Sotuvchi","Active":"tYES"}
```

### Items

Manba: EDMX, cookbook namunasi. Qoldiq so'rovda yuborilsa ham e'tiborsiz qoldiriladi.

```http
POST /b1s/v2/Items
{"ItemCode":"I0001","ItemName":"Issiq non","InventoryItem":"tYES","SalesItem":"tYES","PurchaseItem":"tYES"}

HTTP/1.1 201 Created
Location: .../b1s/v2/Items('I0001')

{
  "ItemCode": "I0001",
  "ItemName": "Issiq non",
  "InventoryItem": "tYES",
  "SalesItem": "tYES",
  "PurchaseItem": "tYES",
  "QuantityOnStock": 0,
  "ItemWarehouseInfoCollection": [
    {"ItemCode": "I0001", "WarehouseCode": "01", "InStock": 0}
  ]
}
```

### PurchaseInvoices

Manba: PDF s.25 (`Orders` bilan bir xil shakl), EDMX. `DocTotal` qatorlardan hisoblanadi.

```http
POST /b1s/v2/PurchaseInvoices
{"CardCode":"S0001","DocDate":"2026-11-01","DocDueDate":"2026-11-30",
 "DocumentLines":[{"ItemCode":"I0001","Quantity":100,"UnitPrice":5,"WarehouseCode":"01"}]}

HTTP/1.1 201 Created
Location: .../b1s/v2/PurchaseInvoices(1)

{
  "DocEntry": 1,
  "DocNum": 2001,
  "DocType": "dDocument_Items",
  "DocDate": "2026-11-01T00:00:00Z",
  "DocDueDate": "2026-11-30T00:00:00Z",
  "CardCode": "S0001",
  "CardName": "Ta'minotchi",
  "DocTotal": 500,
  "PaidToDate": 0,
  "DocumentStatus": "bost_Open",
  "Cancelled": "tNO",
  "CancelStatus": "csNo",
  "DocumentsOwner": null,
  "SalesPersonCode": -1,
  "PaymentGroupCode": -1,
  "NumberOfInstallments": 1,
  "Comments": null,
  "DocumentLines": [
    {
      "LineNum": 0,
      "ItemCode": "I0001",
      "ItemDescription": "Issiq non",
      "Quantity": 100,
      "Price": 5,
      "UnitPrice": 5,
      "LineTotal": 500,
      "WarehouseCode": "01",
      "TaxCode": null,
      "VatGroup": null,
      "LineStatus": "bost_Open"
    }
  ]
}
```

Xariddan keyin qoldiq oshadi:

```http
GET /b1s/v2/Items('I0001')?$select=ItemCode,QuantityOnStock,ItemWarehouseInfoCollection

{"ItemCode":"I0001","QuantityOnStock":100,"ItemWarehouseInfoCollection":[{"ItemCode":"I0001","WarehouseCode":"01","InStock":100}]}
```

### Invoices

Manba: PDF s.25, EDMX. Sarlavha va `DocumentLines` xariddagi kabi, qo'shimcha `DocumentInstallments`. Xodim `DocumentsOwner` orqali biriktiriladi.

```http
POST /b1s/v2/Invoices
{"CardCode":"C0001","DocDate":"2026-11-02","DocDueDate":"2026-12-02","DocumentsOwner":1,
 "DocumentLines":[{"ItemCode":"I0001","Quantity":25,"UnitPrice":8,"WarehouseCode":"01"}],
 "DocumentInstallments":[{"DueDate":"2026-12-02","Percentage":50},{"DueDate":"2027-01-02","Percentage":50}]}

HTTP/1.1 201 Created
Location: .../b1s/v2/Invoices(1)

{
  "DocEntry": 1,
  "DocNum": 1001,
  "DocTotal": 200,
  "PaidToDate": 0,
  "DocumentStatus": "bost_Open",
  "DocumentsOwner": 1,
  "NumberOfInstallments": 2,
  "DocumentLines": [ ... ],
  "DocumentInstallments": [
    {"InstallmentId": 1, "DueDate": "2026-12-02T00:00:00Z", "Total": 100, "Percentage": 50},
    {"InstallmentId": 2, "DueDate": "2027-01-02T00:00:00Z", "Total": 100, "Percentage": 50}
  ]
}
```

### IncomingPayments

Manba: EDMX, SDK, cookbook namunasi. `PaymentInvoices` uchun tayyor namuna topilmadi, shakl EDMX'dan.

```http
POST /b1s/v2/IncomingPayments
{"CardCode":"C0001","DocDate":"2026-11-03","CashAccount":"1011","CashSum":120,
 "PaymentInvoices":[{"DocEntry":1,"SumApplied":120,"InvoiceType":"it_Invoice"}]}

HTTP/1.1 201 Created
Location: .../b1s/v2/IncomingPayments(1)

{
  "DocEntry": 1,
  "DocNum": 3001,
  "DocType": "rCustomer",
  "DocTypte": "rCustomer",
  "DocDate": "2026-11-03T00:00:00Z",
  "CardCode": "C0001",
  "CardName": "Ali Valiyev",
  "CashAccount": "1011",
  "CashSum": 120,
  "Cancelled": "tNO",
  "Remarks": null,
  "PaymentInvoices": [
    {"LineNum": 0, "DocEntry": 1, "SumApplied": 120, "InvoiceType": "it_Invoice", "InstallmentId": 1}
  ]
}
```

Qisman to'lovdan keyin sotuv ochiq qoladi, qarz kamayadi:

```http
GET /b1s/v2/Invoices(1)?$select=DocEntry,DocTotal,PaidToDate,DocumentStatus
{"DocEntry":1,"DocTotal":200,"PaidToDate":120,"DocumentStatus":"bost_Open"}

GET /b1s/v2/BusinessPartners('C0001')?$select=CardCode,CurrentAccountBalance
{"CardCode":"C0001","CurrentAccountBalance":80}
```

### VendorPayments

`IncomingPayments` bilan bir xil shakl. To'liq to'langan xarid yopiladi.

```http
POST /b1s/v2/VendorPayments
{"CardCode":"S0001","DocDate":"2026-11-03","CashAccount":"1011","CashSum":500,
 "PaymentInvoices":[{"DocEntry":1,"SumApplied":500,"InvoiceType":"it_PurchaseInvoice"}]}

HTTP/1.1 201 Created
{"DocEntry":1,"DocNum":4001,"DocType":"rSupplier","CashSum":500, ... }

GET /b1s/v2/PurchaseInvoices(1)?$select=DocEntry,DocTotal,PaidToDate,DocumentStatus
{"DocEntry":1,"DocTotal":500,"PaidToDate":500,"DocumentStatus":"bost_Close"}
```

### Standart hisoblar

Manba: EDMX, cookbook (`GET CompanyService_GetPeriods`, `POST CompanyService_GetPeriod`), SDK. Javob shakli EDMX'dan xulosa.

```http
GET /b1s/v2/CompanyService_GetPeriods
{"value":[{"AbsoluteEntry":1}]}

POST /b1s/v2/CompanyService_GetPeriod
{"PeriodCategoryParams":{"AbsoluteEntry":1}}

{
  "AbsoluteEntry": 1,
  "PeriodCategoryProperty": "2026",
  "BeginningofFinancialYear": "2026-01-01T00:00:00Z",
  "DebitorsFollowUpAccount": "1200",
  "AccountforCashReceipt": "1010",
  "DefaultSaleAccount": "4000",
  "ExpenseAccountDefault": "5000"
}
```

### Actions va hujjat xatolari

`Cancel` shakli PDF s.164 dan, `Close` s.29 dan, `DELETE` xatosi s.28 dan. Qolgan matnlar mock tanlovi.

```http
DELETE /b1s/v2/Invoices(1)
HTTP/1.1 400 Bad Request
{"error":{"code":"-5006","message":"The requested action is not supported for this object."}}

POST /b1s/v2/Invoices(1)/Cancel
HTTP/1.1 400 Bad Request
{"error":{"code":"-10","message":"Cancel the payments of this document first"}}

POST /b1s/v2/IncomingPayments(1)/Cancel
HTTP/1.1 204 No Content

POST /b1s/v2/Invoices(1)/Cancel
HTTP/1.1 204 No Content

GET /b1s/v2/Invoices(1)?$select=DocEntry,Cancelled,CancelStatus,DocumentStatus
{"DocEntry":1,"Cancelled":"tYES","CancelStatus":"csYes","DocumentStatus":"bost_Close"}

POST /b1s/v2/Invoices
{"CardCode":"C0001","DocumentLines":[{"ItemCode":"I0001","Quantity":1000,"UnitPrice":8}]}
HTTP/1.1 400 Bad Request
{"error":{"code":"-10","message":"Quantity falls into negative inventory for item 'I0001' in warehouse '01'"}}
```

### UserFieldsMD

Manba: PDF s.87-89. Mock javobida `ValidValuesMD` yo'q (6-bo'lim).

```http
POST /b1s/v2/UserFieldsMD
{"Name":"Region","Type":"db_Alpha","Size":50,"Description":"Hudud","SubType":"st_None","TableName":"OCRD"}

HTTP/1.1 201 Created
Location: .../b1s/v2/UserFieldsMD(TableName='OCRD',FieldID=0)

{
  "Name": "Region",
  "Type": "db_Alpha",
  "Size": 50,
  "Description": "Hudud",
  "SubType": "st_None",
  "LinkedTable": null,
  "DefaultValue": null,
  "TableName": "OCRD",
  "FieldID": 0,
  "EditSize": 50,
  "Mandatory": "tNO",
  "LinkedUDO": null
}
```

```http
GET /b1s/v2/UserFieldsMD(TableName='OCRD', FieldID=0)
GET /b1s/v2/UserFieldsMD?$filter=Name eq 'Region'

PATCH /b1s/v2/UserFieldsMD(TableName='OITM',FieldID=0)
{"EditSize":20,"Description":"Internal Id"}
HTTP/1.1 204 No Content

DELETE /b1s/v2/UserFieldsMD(TableName='OITM',FieldID=4)
HTTP/1.1 204 No Content
```

Xatolar. Hammasi 400, matnlari mock tanlovi:

```json
{"error":{"code":"-10","message":"Field 'U_Region' already exists in table 'OCRD'"}}
{"error":{"code":"-10","message":"'a b' is not a valid field name: use letters, digits and underscores, starting with a letter"}}
{"error":{"code":"-10","message":"User-defined fields cannot be added to table 'OUSR'"}}
{"error":{"code":"-10","message":"The size of a text field must be between 1 and 254"}}
{"error":{"code":"-10","message":"The size cannot be smaller than the current size 20"}}
```

### UDF oddiy maydondek

Manba: PDF s.89-90, hujjat vazifasi. Maydon yaratilishi bilan mock'da ishlaydi. Real SAP'da Service Layer'ni qayta ishga tushirish kerak bo'lishi mumkin (6-ziddiyat).

```http
PATCH /b1s/v2/BusinessPartners('R01')
{"U_Region":"Toshkent"}
HTTP/1.1 204 No Content

GET /b1s/v2/BusinessPartners?$select=CardCode,U_Region&$filter=U_Region eq 'Toshkent'

{
  "@odata.context": ".../b1s/v2/$metadata#BusinessPartners",
  "value": [
    {"CardCode": "R01", "U_Region": "Toshkent"},
    {"CardCode": "R02", "U_Region": "Toshkent"},
    {"CardCode": "R04", "U_Region": "Toshkent"},
    {"CardCode": "R07", "U_Region": "Toshkent"},
    {"CardCode": "R10", "U_Region": "Toshkent"}
  ]
}

GET /b1s/v2/BusinessPartners?$filter=U_Nope eq 'x'
HTTP/1.1 400 Bad Request
{"error":{"code":"-1000","message":"Property 'U_Nope' of 'BusinessPartner' is invalid"}}
```

### `$expand`

Manba: hujjat, PDF s.50-51, s.83.

```http
GET /b1s/v2/Invoices(1)?$select=DocEntry,DocNum,DocTotal&$expand=BusinessPartner($select=CardName,Phone1)

{
  "@odata.context": ".../b1s/v2/$metadata#Invoices/$entity",
  "DocEntry": 1,
  "DocNum": 1001,
  "DocTotal": 80,
  "BusinessPartner": {"CardName": "Ali Valiyev", "Phone1": "+998901112233"}
}

GET /b1s/v2/PurchaseInvoices(1)?$select=DocEntry,DocTotal&$expand=BusinessPartner($select=CardName,Phone1)

{"DocEntry":1,"DocTotal":500,"BusinessPartner":{"CardName":"Taminotchi","Phone1":"+998901112233"}}

GET /b1s/v2/IncomingPayments(1)?$select=DocEntry,CashSum&$expand=BusinessPartner($select=CardName,Phone1)

{"DocEntry":1,"CashSum":20,"BusinessPartner":{"CardName":"Ali Valiyev","Phone1":"+998901112233"}}

GET /b1s/v2/Invoices?$select=DocEntry&$expand=BusinessPartner($select=CardName)&$filter=CardCode eq 'X1'

{
  "value": [
    {"DocEntry": 1, "BusinessPartner": {"CardName": "Ali Valiyev"}},
    {"DocEntry": 2, "BusinessPartner": {"CardName": "Ali Valiyev"}}
  ]
}

GET /b1s/v2/Invoices(1)?$expand=Nope
HTTP/1.1 400 Bad Request
{"error":{"code":"-1000","message":"Property 'Nope' of 'Document' is invalid"}}
```

### SQLQueries

Manba: hujjat, PDF s.141-145, s.152-153. Quyidagilar do'kon ma'lumotidagi haqiqiy javoblar.

```http
POST /b1s/v2/SQLQueries
{"SqlCode":"purch_lines","SqlName":"Xaridlar qatorlari bilan",
 "SqlText":"SELECT T0.DocNum, T0.DocDate, T0.CardCode, T1.ItemCode, T1.Quantity, T1.Price FROM OPCH T0 INNER JOIN PCH1 T1 ON T0.DocEntry = T1.DocEntry WHERE T0.DocDate >= :fromDate"}

HTTP/1.1 201 Created
Location: .../b1s/v2/SQLQueries('purch_lines')

{
  "SqlCode": "purch_lines",
  "SqlName": "Xaridlar qatorlari bilan",
  "SqlText": "SELECT T0.\"DocNum\", T0.\"DocDate\", T0.\"CardCode\", T1.\"ItemCode\", T1.\"Quantity\", T1.\"Price\" FROM \"OPCH\" T0 INNER JOIN \"PCH1\" T1 ON T0.\"DocEntry\" = T1.\"DocEntry\" WHERE T0.\"DocDate\" >= :fromDate",
  "ParamList": "fromDate",
  "CreateDate": "2026-11-06T00:00:00Z",
  "UpdateDate": "2026-11-06T00:00:00Z"
}
```

```http
POST /b1s/v2/SQLQueries('purch_lines')/List
{"ParamList":"fromDate='2026-10-01'"}

GET /b1s/v2/SQLQueries('purch_lines')/List?fromDate='2026-10-01'
Prefer: odata.maxpagesize=4

HTTP/1.1 200 OK
Preference-Applied: odata.maxpagesize=4

{
  "@odata.context": ".../b1s/v2/$metadata#SAPB1.SQLQueryResult",
  "SqlText": "SELECT T0.\"DocNum\", ... WHERE T0.\"DocDate\" >= :fromDate",
  "value": [
    {"DocNum": 2001, "DocDate": "20261003", "CardCode": "GS01", "ItemCode": "GI01", "Quantity": 200, "Price": 5},
    ...
  ],
  "@odata.nextLink": "SQLQueries('purch_lines')/List?fromDate=%272026-10-01%27&$skip=4"
}
```

Xatolar, hammasi yuqoridagi jadvaldagidek:

```http
POST /b1s/v2/SQLQueries('by_item')/List
{"ParamList":"itemCode='GI01';truncate table OITM"}
HTTP/1.1 400 Bad Request
{"error":{"code":"704","message":"Parameter error."}}

POST /b1s/v2/SQLQueries
{"SqlCode":"x","SqlText":"select USER_CODE, PASSWORD from OUSR"}
HTTP/1.1 400 Bad Request
{"error":{"code":"702","message":"Table 'OUSR' not accessible"}}

GET /b1s/v2/SQLQueries('purch_lines_all')/List     (oddiy foydalanuvchi)
HTTP/1.1 403 Forbidden
{"error":{"code":"-6006","message":"You are not permitted to perform this action"}}
```

## 8. Backend Service Layer'dan nimani kutadi

Real SAP'ga ulanganda aynan shular ishlashi kerak. Tekshiruvda muammo chiqsa, avval shu ro'yxatga qarang. Kod: `api/src/Sap.Infrastructure/ServiceLayer/ServiceLayerClient.cs`.

| Backend nima qiladi | Tayangan manba | Real SAP'da farq chiqsa |
|---|---|---|
| `POST Login` body: `CompanyDB`, `UserName`, `Password` | PDF s.16 | Login ishlamaydi |
| Javobdan `SessionId` va `SessionTimeout` o'qiydi | PDF s.16, EDMX `B1Session` | Login ishlamaydi |
| `ROUTEID`ni `Set-Cookie`dan oladi, topilmasa, usiz ishlaydi | PDF s.16-17: ixtiyoriy | Muammo yo'q |
| Har so'rovga `Cookie: B1SESSION=...; ROUTEID=...` | PDF s.16-17 | Hamma so'rov 401 |
| HTTP 401 = sessiya tugagan. Kodga qaramaydi | PDF s.17 | Agar boshqa status qaytsa, sessiya tugashi xato sifatida ko'rinadi |
| Filtr: `CardType eq 'cCustomer'`, `contains(CardName,'x')`, `contains(Phone1,'x')`, `contains(Address,'x')` | PDF s.32-34 | Ro'yxat xato beradi |
| `$filter`, `$orderby`, `$top`, `$skip` nomlaridagi `$` kodlanmaydi, faqat qiymatlar kodlanadi | SAP KBA: kodlangan `$` xato beradi (qidiruv natijasidan, to'liq matn o'qilmagan) | Ro'yxat xato beradi |
| `Prefer: odata.maxpagesize=N`, `$top=N`, `N = pageSize + 1` | PDF s.35 | `hasNextPage` noto'g'ri bo'ladi |
| `value` massivini o'qiydi, `@odata.nextLink`ga tayanmaydi | PDF s.35 | Muammo yo'q |
| `$select` ishlatmaydi | Qaror: `U_Region` real bazada hali yo'q bo'lishi mumkin | Javob katta bo'ladi, lekin ishlaydi |
| Javobdan `U_Region`ni o'qiydi, yo'q bo'lsa `region: null` | Hujjat, PDF s.89-90 | `region` bo'sh chiqadi, ro'yxat ishlaydi |
| `region` berilsa, `U_Region eq 'x'` qo'shiladi, `'` ikkilanadi | Hujjat, PDF s.32-34 | `U_Region` hali yo'q bo'lsa, Service Layer xatosi qaytariladi |
| Javobdagi noma'lum maydonlarni e'tiborsiz qoldiradi | Real obyektda yuzlab maydon bor (EDMX) | Muammo yo'q |
| `CardType`ni `cCustomer` deb taqqoslaydi | PDF s.24: javobda enum nomi | `PATCH` va `DELETE` hammasini 404 qiladi |
| Kalit: `BusinessPartners('C0001')`, `'` ikkilanadi, qiymat kodlanadi | PDF s.26 | Maxsus belgili kodlar ishlamaydi |
| `POST` 201 va obyekt, `PATCH` va `DELETE` 204 | PDF s.24, s.27 | Xato sifatida ko'rinadi |
| Xato: `error.code` son yoki matn, `error.message` matn yoki `{value}` | PDF s.25, s.162 | Status baribir saqlanadi |
| Body `Content-Length` bilan yuboriladi, chunked emas | Qaror: eng xavfsiz variant | Muammo kutilmaydi |

## 9. Do'kon skripti Service Layer'dan nimani kutadi

Real SAP'da skript to'xtasa, avval shu ro'yxatga qarang. Kod: `tools/GroceryStore/ScenarioRunner.cs`. Muhitga xos qiymatlar `.env`da.

| Qadam | Skript yuboradi | Sozlama | Xavf |
|---|---|---|---|
| Login | `CompanyDB`, `UserName`, `Password` | `SL_COMPANY_DB`, `SL_USERNAME`, `SL_PASSWORD` | |
| Kassa hisobi | `FormatCode`, `Name`, `FatherAccountKey`, `CashAccount`, `ActiveAccount`. Javobdagi `Code` ishlatiladi | `GROCERY_CASH_PARENT_ACCOUNT` | Ota hisob sarlavha hisobi bo'lishi kerak (19-savol) |
| Hamkor | `CardCode`, `CardName`, `CardType`, `Phone1`, `Address`, `City` | `GROCERY_PREFIX` | Kod band bo'lsa, `-10` |
| Mol | `ItemCode`, `ItemName`, `InventoryItem`, `SalesItem`, `PurchaseItem` | `GROCERY_PREFIX` | |
| Xarid va sotuv | `DocDate`, `DocDueDate`, qatorlar: `ItemCode`, `Quantity`, `UnitPrice`, `WarehouseCode` | `SL_WAREHOUSE`, `SL_TAX_CODE`, `SL_VAT_GROUP`, `GROCERY_CURRENT_MONTH` | Soliq kodi lokalizatsiyaga bog'liq. Davr yopiq yoki sana kelajakda bo'lishi mumkin (16-savol) |
| Xodim | `FirstName`, `LastName`, `JobTitle` | | |
| Sotuvga xodim | `DocumentsOwner` | | |
| Oyma-oy to'lov | `DocumentInstallments`: `DueDate`, `Percentage` | | Eng katta xavf: real SAP to'lov shartlarini talab qilishi mumkin (6-savol) |
| To'lov | `CashAccount`, `CashSum`, `PaymentInvoices`: `DocEntry`, `SumApplied`, `InvoiceType`. Summa javobdagi `DocTotal`dan | | |
| Bekor qilish | `Invoices(n)/Cancel`, `IncomingPayments(n)/Cancel` | | Qo'shimcha hujjat paydo bo'lishi mumkin (17-savol) |
| Tekshiruv | `Items`: `QuantityOnStock`. `BusinessPartners`: `CurrentAccountBalance`. `Invoices`: `DocumentStatus`, `PaidToDate` | | Skript hisobi javoblardan olinadi, soliq formulani buzmaydi |

## 10. Mentor uchun tekshiruv ro'yxati

Hujjatga ko'ra real SAP test bazasida, ishchi bazada emas. Har topilgan farq (maydon nomi, sana formati, xato matni) qaytariladi: mock tuzatiladi va shu fayl yangilanadi. Kod faqat sozlama bilan ulanishi kerak.

**Tayyorgarlik**

1. `postman/real-sap.postman_environment.json`ni to'ldiring: `baseUrl`, `CompanyDB`, `UserName`, `Password`, `normalUser`, `normalPassword` (SQL ruxsati yo'q foydalanuvchi), `parentAccount` (sarlavha hisobi).
2. `.env`: `SL_BASE_URL`, `SL_COMPANY_DB`, `SL_SKIP_TLS_VERIFY=true`. Do'kon skripti uchun yana `SL_USERNAME`, `SL_PASSWORD`, `SL_WAREHOUSE`, `SL_TAX_CODE` yoki `SL_VAT_GROUP`, `GROCERY_CASH_PARENT_ACCOUNT`, `GROCERY_CURRENT_MONTH`.
3. `docker compose up --build api`.

**Hujjatdagi tekshiruvlar**

| # | Nima | Qanday | Kutiladi | Farq chiqishi mumkin bo'lgan joy |
|---|---|---|---|---|
| 1 | Login, sessiya | Postman `01 Auth` | 200, `B1SESSION`, xato login 401 | Xato login matni (3-savol) |
| 2 | Mijozlar CRUD | `03 BusinessPartners CRUD` | 201, 204, dublikat -10 | Majburiy maydonlar, xato matni (5-savol) |
| 3 | Filtrlar, sahifalash | `04 OData queries` | Aniq qatorlar, `@odata.nextLink` | `$top` bilan birga (2-savol), `contains` harfi (13-savol) |
| 4 | Backend | `05 Backend API`, `.env` almashtirilgan | Token, `items`, 401 da `SESSION_EXPIRED` | 8-bo'lim |
| 5 | Sotuv va to'lov | `06`, `07` papkalar | `DocTotal`, qoldiq, `PaidToDate`, yopilish | Soliq (summalar), oyma-oy to'lov (6-savol), bekor qilish (17-savol) |
| 6 | Do'kon skripti | `docker compose run --rm --build grocery-store` | `ACCEPTED` | 9-bo'lim |
| 7 | `$expand`, UDF | `08 Expand and UDF` | Ichma-ich obyekt, `U_Region` filtri | UDF uchun qayta ishga tushirish (6-ziddiyat), `IncomingPayments` (1-ziddiyat) |
| 8 | SQL fayllar | `sql/01-14` Query Manager'da | So'rovlar xatosiz ishlaydi | HANA'da sana solishtirish (31-savol), `[%0]` yozilishi (9-savol) |
| 9 | `SqlText` | `09 SQLQueries`, vazifadagi matn | `purch_lines` sana bilan va sanasiz ishlaydi | Sana parametri (37-savol), ruxsat (`-6006`) |

**Mentordan aniq so'raladigan savollar**

1. `CUFD.TypeID` va `db_Float` qiymati nima? (23-savol)
2. Bekor qilingan sotuvda alohida `CANCELED = 'C'` hujjat paydo bo'ladimi? (17-savol)
3. `DocumentInstallments` to'lov shartisiz qabul qilinadimi? (6-savol)
4. Kelajak sanali hujjat o'tadimi? (16-savol)
5. Yangi UDF qayta ishga tushirishsiz ko'rinadimi? (6-ziddiyat)
6. `$expand=BusinessPartner` `IncomingPayments`da ishlaydimi? (29-savol)
7. Ta'minotchi balansi xaridda qaysi belgida? (8-savol)
8. `[%0]` qaysi yozuvda ishlaydi? (9-savol)
