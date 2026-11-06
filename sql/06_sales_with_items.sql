-- Task 6. Har bir sotuv, sotilgan mollari bilan.
-- One row per sold item, so a sale with three lines appears three times.
--
-- Expected on the grocery store data, as psql prints it (19 rows):
--   DocEntry | DocNum | CardCode | LineNum | ItemCode | Dscription | Quantity | Price | LineTotal
-- > 1 | 1001 | GC01 | 0 | GI01 | Issiq non | 100.000000 | 8.000000 | 800.000000
-- > 2 | 1002 | GC02 | 0 | GI02 | Sut | 10.000000 | 14.000000 | 140.000000
-- > 3 | 1003 | GC03 | 0 | GI03 | Guruch | 20.000000 | 28.000000 | 560.000000
-- > 3 | 1003 | GC03 | 1 | GI01 | Issiq non | 10.000000 | 8.000000 | 80.000000
-- > 4 | 1004 | GC04 | 0 | GI04 | Go'sht | 3.000000 | 85.000000 | 255.000000
-- > 5 | 1005 | GC05 | 0 | GI05 | Shakar | 40.000000 | 16.000000 | 640.000000
-- > 6 | 1006 | GC06 | 0 | GI06 | Choy | 20.000000 | 35.000000 | 700.000000
-- > 6 | 1006 | GC06 | 1 | GI02 | Sut | 10.000000 | 14.000000 | 140.000000
-- > 6 | 1006 | GC06 | 2 | GI04 | Go'sht | 2.000000 | 85.000000 | 170.000000
-- > 7 | 1007 | GC07 | 0 | GI07 | O'simlik yog'i | 5.000000 | 42.000000 | 210.000000
-- > 8 | 1008 | GC08 | 0 | GI08 | Tuxum | 50.000000 | 3.000000 | 150.000000
-- > 9 | 1009 | GC09 | 0 | GI09 | Olma | 50.000000 | 13.000000 | 650.000000
-- > 10 | 1010 | GC10 | 0 | GI10 | Qatiq | 20.000000 | 11.000000 | 220.000000
-- > 10 | 1010 | GC10 | 1 | GI05 | Shakar | 5.000000 | 16.000000 | 80.000000
-- > 11 | 1011 | GC01 | 0 | GI01 | Issiq non | 20.000000 | 8.000000 | 160.000000
-- > 12 | 1012 | GC01 | 0 | GI09 | Olma | 10.000000 | 13.000000 | 130.000000
-- > 13 | 1013 | GC03 | 0 | GI03 | Guruch | 10.000000 | 28.000000 | 280.000000
-- > 14 | 1014 | GC05 | 0 | GI06 | Choy | 5.000000 | 35.000000 | 175.000000
-- > 16 | 1016 | GC02 | 0 | GI02 | Sut | 5.000000 | 14.000000 | 70.000000

SELECT T0."DocEntry", T0."DocNum", T0."CardCode", T1."LineNum", T1."ItemCode", T1."Dscription", T1."Quantity", T1."Price", T1."LineTotal"
FROM "OINV" T0
INNER JOIN "INV1" T1 ON T1."DocEntry" = T0."DocEntry"
WHERE T0."CANCELED" = 'N'
ORDER BY T0."DocEntry", T1."LineNum";
