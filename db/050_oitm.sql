-- Items. OnHand is the total; it changes only through documents.
CREATE TABLE "OITM" (
    "ItemCode"   VARCHAR(50)    PRIMARY KEY,
    "ItemName"   VARCHAR(200),
    "InvntItem"  VARCHAR(1)     NOT NULL DEFAULT 'Y' CHECK ("InvntItem" IN ('Y', 'N')),
    "SellItem"   VARCHAR(1)     NOT NULL DEFAULT 'Y' CHECK ("SellItem" IN ('Y', 'N')),
    "PrchseItem" VARCHAR(1)     NOT NULL DEFAULT 'Y' CHECK ("PrchseItem" IN ('Y', 'N')),
    "OnHand"     NUMERIC(19, 6) NOT NULL DEFAULT 0
);

-- Stock per warehouse. The rows of one item add up to OITM.OnHand.
CREATE TABLE "OITW" (
    "ItemCode" VARCHAR(50)    NOT NULL REFERENCES "OITM" ("ItemCode") ON DELETE CASCADE,
    "WhsCode"  VARCHAR(8)     NOT NULL,
    "OnHand"   NUMERIC(19, 6) NOT NULL DEFAULT 0,
    PRIMARY KEY ("ItemCode", "WhsCode")
);
