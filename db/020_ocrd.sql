-- Business partners: customers, suppliers and leads in one table, told apart by CardType.
-- Column names and sizes follow the SDK table reference. Names are quoted to keep their SAP spelling.
CREATE TABLE "OCRD" (
    "CardCode" VARCHAR(15)   PRIMARY KEY,
    "CardName" VARCHAR(200),
    "CardType" VARCHAR(1)    NOT NULL DEFAULT 'C' CHECK ("CardType" IN ('C', 'S', 'L')),
    "Phone1"   VARCHAR(50),
    "Cellular" VARCHAR(50),
    "E_Mail"   VARCHAR(100),
    "Address"  VARCHAR(100),
    "City"     VARCHAR(100),
    "Balance"  NUMERIC(19, 6) NOT NULL DEFAULT 0
);
