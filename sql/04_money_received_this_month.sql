-- Task 4. Shu oyda kelgan pul (statik sanalar).
-- This month is 2026-11, the GROCERY_CURRENT_MONTH of the store data. The dates are static on purpose.
-- ORCT is incoming payments, so the column name differs from OINV: Canceled, not CANCELED.
-- COALESCE gives 0 instead of an empty value for a month with no payments.
--
-- Expected on the grocery store data, as psql prints it (1 row):
--   ReceivedTotal
-- > 2176.000000

SELECT COALESCE(SUM(T0."DocTotal"), 0) AS "ReceivedTotal"
FROM "ORCT" T0
WHERE T0."Canceled" = 'N'
  AND T0."DocDate" >= '2026-11-01'
  AND T0."DocDate" < '2026-12-01';
