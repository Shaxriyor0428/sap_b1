-- Task 7. Eng qimmat sotilgan mol (qatordagi Price bo'yicha).
-- DocEntry and LineNum only make the order fixed when two lines have the same price.
--
-- Expected on the grocery store data, as psql prints it (1 row):
--   ItemCode | Dscription | Price
-- > GI04 | Go'sht | 85.000000

SELECT T1."ItemCode", T1."Dscription", T1."Price"
FROM "OINV" T0
INNER JOIN "INV1" T1 ON T1."DocEntry" = T0."DocEntry"
WHERE T0."CANCELED" = 'N'
ORDER BY T1."Price" DESC, T0."DocEntry", T1."LineNum"
LIMIT 1;
