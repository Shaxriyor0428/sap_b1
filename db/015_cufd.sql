-- Metadata of user-defined fields. POST UserFieldsMD writes a row here and adds the U_ column
-- to the table it names. Column names and sizes follow the SDK table reference.
CREATE TABLE "CUFD" (
    "TableID"  VARCHAR(21) NOT NULL,
    "FieldID"  INT         NOT NULL,
    "AliasID"  VARCHAR(50) NOT NULL,
    "Descr"    VARCHAR(80),
    "TypeID"   VARCHAR(1)  NOT NULL,
    "EditType" VARCHAR(1)  NOT NULL DEFAULT '',
    "SizeID"   INT         NOT NULL DEFAULT 0,
    "EditSize" INT         NOT NULL DEFAULT 0,
    "Dflt"     VARCHAR(254),
    "NotNull"  VARCHAR(1)  NOT NULL DEFAULT 'N' CHECK ("NotNull" IN ('Y', 'N')),
    "RTable"   VARCHAR(21),
    "RelUDO"   VARCHAR(20),
    PRIMARY KEY ("TableID", "FieldID"),
    UNIQUE ("TableID", "AliasID")
);
