-- Task 10. Eng ko'p marta xarid qilgan 10 ta mijoz.
-- Counts sales, not lines. With the cancelled sale, GC07 would show 2.
--
-- Expected on the grocery store data, as psql prints it (10 rows):
--   CardCode | CardName | SalesCount
-- > GC01 | Ali Valiyev | 3
-- > GC02 | Bobur Toshev | 2
-- > GC03 | Dilshod Ergashev | 2
-- > GC05 | Farrux Rahmonov | 2
-- > GC04 | Eldor Rasulov | 1
-- > GC06 | Gulnora Saidova | 1
-- > GC07 | Hamid Ismoilov | 1
-- > GC08 | Ibrohim Nazarov | 1
-- > GC09 | Jasur Umarov | 1
-- > GC10 | Komil Sobirov | 1

SELECT T0."CardCode", T1."CardName", COUNT(*) AS "SalesCount"
FROM "OINV" T0
INNER JOIN "OCRD" T1 ON T1."CardCode" = T0."CardCode"
WHERE T0."CANCELED" = 'N'
GROUP BY T0."CardCode", T1."CardName"
ORDER BY "SalesCount" DESC, T0."CardCode"
LIMIT 10;
