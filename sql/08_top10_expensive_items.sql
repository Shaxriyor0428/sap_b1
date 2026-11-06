-- Task 8. Eng qimmat sotilgan 10 ta mol (har bir mol bir marta).
-- GROUP BY keeps each item once, with its highest price.
-- The name comes from OITM: Dscription can be edited on a line, so grouping by it could show an item twice.
--
-- Expected on the grocery store data, as psql prints it (10 rows):
--   ItemCode | ItemName | MaxPrice
-- > GI04 | Go'sht | 85.000000
-- > GI07 | O'simlik yog'i | 42.000000
-- > GI06 | Choy | 35.000000
-- > GI03 | Guruch | 28.000000
-- > GI05 | Shakar | 16.000000
-- > GI02 | Sut | 14.000000
-- > GI09 | Olma | 13.000000
-- > GI10 | Qatiq | 11.000000
-- > GI01 | Issiq non | 8.000000
-- > GI08 | Tuxum | 3.000000

SELECT T1."ItemCode", T2."ItemName", MAX(T1."Price") AS "MaxPrice"
FROM "OINV" T0
INNER JOIN "INV1" T1 ON T1."DocEntry" = T0."DocEntry"
INNER JOIN "OITM" T2 ON T2."ItemCode" = T1."ItemCode"
WHERE T0."CANCELED" = 'N'
GROUP BY T1."ItemCode", T2."ItemName"
ORDER BY "MaxPrice" DESC, T1."ItemCode"
LIMIT 10;
