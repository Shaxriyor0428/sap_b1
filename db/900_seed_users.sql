-- Test users of the mock. The password is stored as a bcrypt hash.
-- manager can do everything; user1 is the ordinary user needed later for SQLQueries permissions.
INSERT INTO "OUSR" ("USER_CODE", "U_NAME", "PASSWORD", "SUPERUSER") VALUES
    ('manager', 'Manager', crypt('1234', gen_salt('bf')), 'Y'),
    ('user1', 'Ordinary User', crypt('1234', gen_salt('bf')), 'N');
