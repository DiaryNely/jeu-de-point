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
