-- Saved SQL of the SQLQueries object. Names follow the SDK table reference.
-- SqlText holds the normalized text. ParamList is TEXT here, the SDK has 254 characters.
CREATE TABLE "OSQL" (
    "SqlCode"    VARCHAR(254) PRIMARY KEY,
    "SqlName"    VARCHAR(254),
    "SqlText"    TEXT NOT NULL,
    "ParamList"  TEXT,
    "CreateDate" DATE NOT NULL,
    "UpdateDate" DATE NOT NULL
);
