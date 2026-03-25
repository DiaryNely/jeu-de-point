BEGIN;

CREATE SCHEMA IF NOT EXISTS game;
SET search_path TO game, public;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'game_status') THEN
        CREATE TYPE game_status AS ENUM ('pending', 'in_progress', 'finished', 'cancelled');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'move_type') THEN
        CREATE TYPE move_type AS ENUM ('place_point', 'destroy_point', 'draw_line', 'special');
    END IF;
END$$;

CREATE TABLE IF NOT EXISTS players (
    id      BIGSERIAL PRIMARY KEY,
    name    VARCHAR(80) NOT NULL,
    score   INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT uq_players_name UNIQUE (name),
    CONSTRAINT ck_players_score_non_negative CHECK (score >= 0)
);

CREATE TABLE IF NOT EXISTS games (
    id                  BIGSERIAL PRIMARY KEY,
    width               INTEGER NOT NULL,
    height              INTEGER NOT NULL,
    current_player_id   BIGINT NULL,
    status              game_status NOT NULL DEFAULT 'pending',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT ck_games_width_positive CHECK (width > 0),
    CONSTRAINT ck_games_height_positive CHECK (height > 0),
    CONSTRAINT fk_games_current_player
        FOREIGN KEY (current_player_id)
        REFERENCES players(id)
        ON UPDATE CASCADE
        ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS moves (
    id          BIGSERIAL PRIMARY KEY,
    game_id     BIGINT NOT NULL,
    player_id   BIGINT NOT NULL,
    x           INTEGER NOT NULL,
    y           INTEGER NOT NULL,
    type        move_type NOT NULL,
    power       INTEGER NOT NULL DEFAULT 0,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_moves_game
        FOREIGN KEY (game_id)
        REFERENCES games(id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    CONSTRAINT fk_moves_player
        FOREIGN KEY (player_id)
        REFERENCES players(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT ck_moves_x_non_negative CHECK (x >= 0),
    CONSTRAINT ck_moves_y_non_negative CHECK (y >= 0),
    CONSTRAINT ck_moves_power_non_negative CHECK (power >= 0)
);

CREATE TABLE IF NOT EXISTS points (
    id              BIGSERIAL PRIMARY KEY,
    game_id         BIGINT NOT NULL,
    player_id       BIGINT NOT NULL,
    x               INTEGER NOT NULL,
    y               INTEGER NOT NULL,
    is_destroyed    BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT fk_points_game
        FOREIGN KEY (game_id)
        REFERENCES games(id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    CONSTRAINT fk_points_player
        FOREIGN KEY (player_id)
        REFERENCES players(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT ck_points_x_non_negative CHECK (x >= 0),
    CONSTRAINT ck_points_y_non_negative CHECK (y >= 0),

    CONSTRAINT uq_points_game_xy UNIQUE (game_id, x, y)
);

CREATE TABLE IF NOT EXISTS lines (
    id              BIGSERIAL PRIMARY KEY,
    game_id         BIGINT NOT NULL,
    player_id       BIGINT NOT NULL,
    points_count    INTEGER NOT NULL,
    is_validated    BOOLEAN NOT NULL DEFAULT FALSE,
    validated_at    TIMESTAMPTZ NULL,

    CONSTRAINT fk_lines_game
        FOREIGN KEY (game_id)
        REFERENCES games(id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    CONSTRAINT fk_lines_player
        FOREIGN KEY (player_id)
        REFERENCES players(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT ck_lines_points_count_min CHECK (points_count >= 2),
    CONSTRAINT ck_lines_validation_consistency
        CHECK (
            (is_validated = FALSE AND validated_at IS NULL)
            OR
            (is_validated = TRUE AND validated_at IS NOT NULL)
        )
);

CREATE INDEX IF NOT EXISTS ix_games_status_created_at
    ON games (status, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_games_current_player
    ON games (current_player_id);

CREATE INDEX IF NOT EXISTS ix_moves_game_created_at
    ON moves (game_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_moves_game_player
    ON moves (game_id, player_id);

CREATE INDEX IF NOT EXISTS ix_points_game_player
    ON points (game_id, player_id);

CREATE INDEX IF NOT EXISTS ix_points_game_destroyed
    ON points (game_id, is_destroyed);

CREATE INDEX IF NOT EXISTS ix_lines_game_player
    ON lines (game_id, player_id);

CREATE INDEX IF NOT EXISTS ix_lines_game_validated
    ON lines (game_id, is_validated)
    WHERE is_validated = TRUE;

CREATE OR REPLACE FUNCTION fn_check_grid_bounds_points()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    g_width  INTEGER;
    g_height INTEGER;
BEGIN
    SELECT width, height
      INTO g_width, g_height
      FROM games
     WHERE id = NEW.game_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Game % not found for points row', NEW.game_id;
    END IF;

    IF NEW.x >= g_width OR NEW.y >= g_height THEN
        RAISE EXCEPTION 'Point coordinates (%,%) out of bounds for game % (width %, height %)',
            NEW.x, NEW.y, NEW.game_id, g_width, g_height;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_check_grid_bounds_points ON points;
CREATE TRIGGER trg_check_grid_bounds_points
BEFORE INSERT OR UPDATE OF game_id, x, y
ON points
FOR EACH ROW
EXECUTE FUNCTION fn_check_grid_bounds_points();

CREATE OR REPLACE FUNCTION fn_check_grid_bounds_moves()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    g_width  INTEGER;
    g_height INTEGER;
BEGIN
    SELECT width, height
      INTO g_width, g_height
      FROM games
     WHERE id = NEW.game_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Game % not found for moves row', NEW.game_id;
    END IF;

    IF NEW.x >= g_width OR NEW.y >= g_height THEN
        RAISE EXCEPTION 'Move coordinates (%,%) out of bounds for game % (width %, height %)',
            NEW.x, NEW.y, NEW.game_id, g_width, g_height;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_check_grid_bounds_moves ON moves;
CREATE TRIGGER trg_check_grid_bounds_moves
BEFORE INSERT OR UPDATE OF game_id, x, y
ON moves
FOR EACH ROW
EXECUTE FUNCTION fn_check_grid_bounds_moves();

COMMIT;
