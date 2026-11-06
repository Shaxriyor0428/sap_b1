-- What a new SAP company already has: a basic chart of accounts and the default accounts.
-- Everything else, including the shop's cash accounts, is created through the API.
INSERT INTO "OACT" ("AcctCode", "AcctName", "FormatCode", "Postable", "Finanse", "Levels", "FatherNum", "ActType") VALUES
    ('1000', 'Aktivlar', '1000', 'N', 'N', 1, NULL, 'N'),
    ('1010', 'Asosiy kassa', '1010', 'Y', 'Y', 2, '1000', 'N'),
    ('1200', 'Debitorlar', '1200', 'Y', 'N', 2, '1000', 'N'),
    ('4000', 'Sotuvdan tushum', '4000', 'Y', 'N', 1, NULL, 'I'),
    ('5000', 'Xarajatlar', '5000', 'Y', 'N', 1, NULL, 'E');

INSERT INTO "OACP" ("PeriodCat", "FinancYear", "LinkAct_1", "LinkAct_3", "DfltIncom", "DfltExpn") VALUES
    ('2026', '2026-01-01', '1200', '1010', '4000', '5000');
