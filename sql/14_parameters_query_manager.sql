-- Task 14. Qo'shimcha ([%0]): 3-5-vazifalarning ikkinchi varianti, sanalar [%0] va [%1] parametrlari bilan. Bu variant mock'da ishlamaydi, uni mentor real SAP'ning Query Manager'ida tekshiradi.
--
-- [%0] and [%1] are asked from the user when the query runs: the first and the last day of the month.
-- The Query Manager runs one query at a time, so paste them one by one.
-- With 2026-11-01 and 2026-11-30, the store data gives the same results as tasks 3, 4 and 5.
--
-- Not verified: this syntax does not run on the mock, and open sources disagree about HANA.
-- If the Query Manager refuses a query, try '[%0]' and '[%1]' in single quotes.

-- Task 3 with parameters: all sales of the period.
SELECT T0."DocEntry", T0."DocNum", T0."CardCode", T0."DocDate", T0."DocTotal"
FROM "OINV" T0
WHERE T0."CANCELED" = 'N'
  AND T0."DocDate" >= [%0]
  AND T0."DocDate" <= [%1]
ORDER BY T0."DocDate", T0."DocEntry";

-- Task 4 with parameters: money received in the period.
SELECT COALESCE(SUM(T0."DocTotal"), 0) AS "ReceivedTotal"
FROM "ORCT" T0
WHERE T0."Canceled" = 'N'
  AND T0."DocDate" >= [%0]
  AND T0."DocDate" <= [%1];

-- Task 5 with parameters: number of sales in the period.
SELECT COUNT(*) AS "SalesCount"
FROM "OINV" T0
WHERE T0."CANCELED" = 'N'
  AND T0."DocDate" >= [%0]
  AND T0."DocDate" <= [%1];
