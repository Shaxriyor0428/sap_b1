-- Task 5. Shu oydagi sotuvlar soni (statik sanalar).
-- This month is 2026-11, the GROCERY_CURRENT_MONTH of the store data. The dates are static on purpose.
--
-- Expected on the grocery store data, as psql prints it (1 row):
--   SalesCount
-- > 10

SELECT COUNT(*) AS "SalesCount"
FROM "OINV" T0
WHERE T0."CANCELED" = 'N'
  AND T0."DocDate" >= '2026-11-01'
  AND T0."DocDate" < '2026-12-01';
