-- Task 11. Eng ko'p pul sarflagan 10 ta mijoz.
-- Spent means the total of the sales, whether they are paid or not. With the cancelled sale, GC07 would show 294.
--
-- Expected on the grocery store data, as psql prints it (10 rows):
--   CardCode | CardName | TotalSpent
-- > GC01 | Ali Valiyev | 1090.000000
-- > GC06 | Gulnora Saidova | 1010.000000
-- > GC03 | Dilshod Ergashev | 920.000000
-- > GC05 | Farrux Rahmonov | 815.000000
-- > GC09 | Jasur Umarov | 650.000000
-- > GC10 | Komil Sobirov | 300.000000
-- > GC04 | Eldor Rasulov | 255.000000
-- > GC02 | Bobur Toshev | 210.000000
-- > GC07 | Hamid Ismoilov | 210.000000
-- > GC08 | Ibrohim Nazarov | 150.000000

SELECT T0."CardCode", T1."CardName", SUM(T0."DocTotal") AS "TotalSpent"
FROM "OINV" T0
INNER JOIN "OCRD" T1 ON T1."CardCode" = T0."CardCode"
WHERE T0."CANCELED" = 'N'
GROUP BY T0."CardCode", T1."CardName"
ORDER BY "TotalSpent" DESC, T0."CardCode"
LIMIT 10;
