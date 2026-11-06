-- Task 13. Qo'shimcha (LEFT JOIN): birorta ham sotuvi bo'lmagan mijozlar.
-- The CANCELED condition is in ON, not WHERE: a customer whose only sale was cancelled has no sales.
-- In WHERE it would remove the empty rows that LEFT JOIN made, and the answer would always be empty.
-- The database stores 'C', while the Service Layer says cCustomer.
--
-- Expected on the grocery store data, as psql prints it (1 row):
--   CardCode | CardName
-- > GC11 | Laziz Qodirov

SELECT T0."CardCode", T0."CardName"
FROM "OCRD" T0
LEFT JOIN "OINV" T1 ON T1."CardCode" = T0."CardCode" AND T1."CANCELED" = 'N'
WHERE T0."CardType" = 'C'
  AND T1."DocEntry" IS NULL
ORDER BY T0."CardCode";
