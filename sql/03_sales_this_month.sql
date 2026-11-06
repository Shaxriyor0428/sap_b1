-- Task 3. Shu oydagi barcha sotuvlar (sanalar SQL ichida statik yoziladi).
-- This month is 2026-11, the GROCERY_CURRENT_MONTH of the store data. The dates are static on purpose.
-- The end is 'before the first of next month', so a time part on a real date cannot lose the last day.
--
-- Expected on the grocery store data, as psql prints it (10 rows):
--   DocEntry | DocNum | CardCode | DocDate | DocTotal
-- > 6 | 1006 | GC06 | 2026-11-02 | 1010.000000
-- > 7 | 1007 | GC07 | 2026-11-06 | 210.000000
-- > 8 | 1008 | GC08 | 2026-11-10 | 150.000000
-- > 9 | 1009 | GC09 | 2026-11-14 | 650.000000
-- > 10 | 1010 | GC10 | 2026-11-18 | 300.000000
-- > 11 | 1011 | GC01 | 2026-11-20 | 160.000000
-- > 12 | 1012 | GC01 | 2026-11-21 | 130.000000
-- > 13 | 1013 | GC03 | 2026-11-22 | 280.000000
-- > 14 | 1014 | GC05 | 2026-11-23 | 175.000000
-- > 16 | 1016 | GC02 | 2026-11-25 | 70.000000

SELECT T0."DocEntry", T0."DocNum", T0."CardCode", T0."DocDate", T0."DocTotal"
FROM "OINV" T0
WHERE T0."CANCELED" = 'N'
  AND T0."DocDate" >= '2026-11-01'
  AND T0."DocDate" < '2026-12-01'
ORDER BY T0."DocDate", T0."DocEntry";
