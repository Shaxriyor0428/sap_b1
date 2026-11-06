-- Task 1. Umumiy summasi 500 dan katta barcha sotuvlar.
-- Cancelled sales are left out.
--
-- Expected on the grocery store data, as psql prints it (5 rows):
--   DocEntry | DocNum | CardCode | DocDate | DocTotal
-- > 6 | 1006 | GC06 | 2026-11-02 | 1010.000000
-- > 1 | 1001 | GC01 | 2026-10-03 | 800.000000
-- > 9 | 1009 | GC09 | 2026-11-14 | 650.000000
-- > 3 | 1003 | GC03 | 2026-10-12 | 640.000000
-- > 5 | 1005 | GC05 | 2026-10-24 | 640.000000

SELECT T0."DocEntry", T0."DocNum", T0."CardCode", T0."DocDate", T0."DocTotal"
FROM "OINV" T0
WHERE T0."DocTotal" > 500
  AND T0."CANCELED" = 'N'
ORDER BY T0."DocTotal" DESC, T0."DocEntry";
