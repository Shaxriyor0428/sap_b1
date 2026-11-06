-- Task 9. Eng qimmat sotilgan mol, mijoz ismi bilan.
-- Two customers bought this item at this price. LIMIT 1 shows the earlier sale: remove LIMIT to see both.
--
-- Expected on the grocery store data, as psql prints it (1 row):
--   ItemCode | Dscription | Price | CardCode | CardName
-- > GI04 | Go'sht | 85.000000 | GC04 | Eldor Rasulov

SELECT T1."ItemCode", T1."Dscription", T1."Price", T0."CardCode", T2."CardName"
FROM "OINV" T0
INNER JOIN "INV1" T1 ON T1."DocEntry" = T0."DocEntry"
INNER JOIN "OCRD" T2 ON T2."CardCode" = T0."CardCode"
WHERE T0."CANCELED" = 'N'
ORDER BY T1."Price" DESC, T0."DocEntry", T1."LineNum"
LIMIT 1;
