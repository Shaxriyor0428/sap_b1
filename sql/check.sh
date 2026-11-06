#!/usr/bin/env bash
# Runs tasks 1-13 on the mock database and compares each result with the "-- > " lines of its file.
# Task 14 is left out: [%0] works only in the SAP B1 Query Manager.
# The database must hold only the grocery store data, see the README.
cd "$(dirname "$0")/.." || exit 2

failed=0

for file in sql/0*.sql sql/1[0-3]*.sql; do
  expected=$(sed -n 's/^-- > //p' "$file")
  actual=$(docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -At -F " | "' < "$file" 2>&1 | tr -d '\r')

  if [ "$expected" = "$actual" ]; then
    echo "ok    $file"
  else
    echo "DIFF  $file"
    diff <(echo "$expected") <(echo "$actual")
    failed=1
  fi
done

exit $failed
