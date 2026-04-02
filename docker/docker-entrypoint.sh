#!/bin/bash
set -euo pipefail

PGDATA="${PGDATA:-/var/lib/postgresql/roveltia-data}"
POSTGRES_USER="${POSTGRES_USER:-roveltia}"
POSTGRES_DB="${POSTGRES_DB:-roveltia}"
: "${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD (e.g. in .env)}"

sql_escape() {
  printf '%s' "$1" | sed "s/'/''/g"
}

mkdir -p "$PGDATA" /var/run/postgresql
chown -R postgres:postgres "$PGDATA" /var/run/postgresql

init_cluster() {
  su postgres -s /bin/bash -c "initdb -D '$PGDATA' --encoding=UTF8 --locale=C --auth-local=trust --auth-host=scram-sha-256"
  {
    echo "listen_addresses = '127.0.0.1'"
    echo "unix_socket_directories = '/var/run/postgresql'"
  } >>"$PGDATA/postgresql.conf"
  echo "host all all 127.0.0.1/32 scram-sha-256" >>"$PGDATA/pg_hba.conf"
}

if [[ ! -f "$PGDATA/PG_VERSION" ]]; then
  init_cluster
fi

su postgres -s /bin/bash -c "pg_ctl -D '$PGDATA' -l /tmp/postgresql.log -w start"

until su postgres -s /bin/bash -c "pg_isready"; do
  sleep 0.2
done

PW_ESC=$(sql_escape "$POSTGRES_PASSWORD")

if ! su postgres -s /bin/bash -c "psql -d postgres -Atc \"SELECT 1 FROM pg_roles WHERE rolname = '$POSTGRES_USER'\"" | grep -q '^1$'; then
  su postgres -s /bin/bash -c "psql -d postgres -v ON_ERROR_STOP=1 -c \"CREATE USER ${POSTGRES_USER} WITH PASSWORD '${PW_ESC}';\""
fi

if ! su postgres -s /bin/bash -c "psql -d postgres -Atc \"SELECT 1 FROM pg_database WHERE datname = '$POSTGRES_DB'\"" | grep -q '^1$'; then
  su postgres -s /bin/bash -c "psql -d postgres -v ON_ERROR_STOP=1 -c \"CREATE DATABASE ${POSTGRES_DB} OWNER ${POSTGRES_USER};\""
fi

export ConnectionStrings__DefaultConnection="${ConnectionStrings__DefaultConnection:-Host=127.0.0.1;Port=5432;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD}"
export Database__AutoMigrate="${Database__AutoMigrate:-true}"

stop_postgres() {
  su postgres -s /bin/bash -c "pg_ctl -D '$PGDATA' -m fast stop" || true
}

shutdown() {
  if [[ -n "${DOTNET_PID:-}" ]] && kill -0 "$DOTNET_PID" 2>/dev/null; then
    kill -TERM "$DOTNET_PID" 2>/dev/null || true
    wait "$DOTNET_PID" 2>/dev/null || true
  fi
  stop_postgres
}

trap 'shutdown; exit 0' SIGTERM SIGINT

cd /app
dotnet Roveltia.Web.dll &
DOTNET_PID=$!
wait "$DOTNET_PID"
EXIT_CODE=$?
shutdown
exit "$EXIT_CODE"
