CREATE TABLE IF NOT EXISTS roles (
    id          SERIAL PRIMARY KEY,
    name        VARCHAR(32) NOT NULL UNIQUE,
    description TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS users (
    id                   SERIAL PRIMARY KEY,
    username             VARCHAR(64) NOT NULL UNIQUE,
    password_hash        TEXT NOT NULL,
    must_change_password BOOLEAN NOT NULL DEFAULT TRUE,
    is_active            BOOLEAN NOT NULL DEFAULT TRUE,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_login_at        TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS user_roles (
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id INT NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, role_id)
);

CREATE TABLE IF NOT EXISTS user_game_scopes (
    id      SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id VARCHAR(64) NOT NULL,
    UNIQUE (user_id, game_id)
);

CREATE TABLE IF NOT EXISTS game_endpoints (
    id           SERIAL PRIMARY KEY,
    game_id      VARCHAR(64) NOT NULL UNIQUE,
    display_name VARCHAR(128) NOT NULL,
    base_url     TEXT NOT NULL,
    admin_key    TEXT NOT NULL DEFAULT '',
    enabled      BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS audit_logs (
    id            BIGSERIAL PRIMARY KEY,
    user_id       INT NOT NULL,
    username      VARCHAR(64) NOT NULL,
    action        VARCHAR(128) NOT NULL,
    game_id       VARCHAR(64) NULL,
    target        TEXT NULL,
    detail        TEXT NULL,
    success       BOOLEAN NOT NULL DEFAULT TRUE,
    error_message TEXT NULL,
    ip_address    VARCHAR(64) NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_created ON audit_logs (created_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_user ON audit_logs (user_id);
