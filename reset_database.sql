-- Active: 1773918989869@@127.0.0.1@5432@jeu_point@game
-- Active: 1773918989869@@127.0.0.1@5432@jeu_point
BEGIN;

TRUNCATE TABLE
    game.point_ownership_claims,
    game.moves,
    game.points,
    game.lines,
    game.games,
    game.players
RESTART IDENTITY CASCADE;

TRUNCATE TABLE public.game_sessions
RESTART IDENTITY CASCADE;

COMMIT;
