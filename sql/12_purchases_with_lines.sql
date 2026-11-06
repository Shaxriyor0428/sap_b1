-- Task 12. Barcha xaridlar, xarid qatorlari bilan.
-- OPCH and PCH1 have the same structure as OINV and INV1.
--
-- Expected on the grocery store data, as psql prints it (10 rows):
--   DocEntry | DocNum | CardCode | DocDate | LineNum | ItemCode | Dscription | Quantity | Price | LineTotal
-- > 1 | 2001 | GS01 | 2026-10-03 | 0 | GI01 | Issiq non | 200.000000 | 5.000000 | 1000.000000
-- > 2 | 2002 | GS02 | 2026-10-08 | 0 | GI02 | Sut | 100.000000 | 10.000000 | 1000.000000
-- > 3 | 2003 | GS03 | 2026-10-12 | 0 | GI03 | Guruch | 100.000000 | 20.000000 | 2000.000000
-- > 4 | 2004 | GS04 | 2026-10-18 | 0 | GI04 | Go'sht | 50.000000 | 60.000000 | 3000.000000
-- > 5 | 2005 | GS05 | 2026-10-24 | 0 | GI05 | Shakar | 100.000000 | 12.000000 | 1200.000000
-- > 6 | 2006 | GS06 | 2026-11-02 | 0 | GI06 | Choy | 60.000000 | 25.000000 | 1500.000000
-- > 7 | 2007 | GS07 | 2026-11-06 | 0 | GI07 | O'simlik yog'i | 60.000000 | 30.000000 | 1800.000000
-- > 8 | 2008 | GS08 | 2026-11-10 | 0 | GI08 | Tuxum | 300.000000 | 2.000000 | 600.000000
-- > 9 | 2009 | GS09 | 2026-11-14 | 0 | GI09 | Olma | 120.000000 | 9.000000 | 1080.000000
-- > 10 | 2010 | GS10 | 2026-11-18 | 0 | GI10 | Qatiq | 80.000000 | 8.000000 | 640.000000

SELECT T0."DocEntry", T0."DocNum", T0."CardCode", T0."DocDate", T1."LineNum", T1."ItemCode", T1."Dscription", T1."Quantity", T1."Price", T1."LineTotal"
FROM "OPCH" T0
INNER JOIN "PCH1" T1 ON T1."DocEntry" = T0."DocEntry"
WHERE T0."CANCELED" = 'N'
ORDER BY T0."DocEntry", T1."LineNum";
