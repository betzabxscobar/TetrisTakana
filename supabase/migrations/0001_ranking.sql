-- ============================================================================
-- TetrisTakana - ranking global
--
-- Pegar entero en el SQL Editor de Supabase y ejecutar. Se puede volver a
-- ejecutar sin romper nada: todo va con IF NOT EXISTS o recreando.
--
-- Modelo: un jugador (players) tiene muchas partidas (game_sessions). El
-- ranking no es una tabla, es la funcion leaderboard() de mas abajo.
-- ============================================================================


-- --- Modos de juego ---------------------------------------------------------
-- Enum y no texto libre: el Tetris y el match-3 puntuan distinto y sus
-- rankings van separados, asi que un modo mal escrito en el cliente no puede
-- crear una tabla fantasma que nadie mira.

do $$
begin
    if not exists (select 1 from pg_type where typname = 'game_mode') then
        create type public.game_mode as enum ('tetris', 'match3');
    end if;
end
$$;


-- --- Jugadores --------------------------------------------------------------
-- El id es el del usuario de Supabase Auth, incluido el anonimo. Asi el
-- jugador entra sin registrarse y, si mas adelante enlaza un correo, conserva
-- sus partidas: el id no cambia al convertir la cuenta anonima en real.

create table if not exists public.players (
    id uuid primary key references auth.users (id) on delete cascade,
    display_name text not null
        check (char_length(btrim(display_name)) between 1 and 16),
    created_at timestamptz not null default now()
);


-- --- Partidas ---------------------------------------------------------------
-- Una fila por partida terminada. No se actualizan ni se borran: una partida
-- jugada es un hecho, y sin UPDATE no hay forma de inflar una puntuacion vieja.

create table if not exists public.game_sessions (
    id uuid primary key default gen_random_uuid(),
    player_id uuid not null references public.players (id) on delete cascade,
    mode public.game_mode not null,
    score integer not null check (score >= 0),
    lines integer not null default 0 check (lines >= 0),
    level integer not null default 1 check (level between 1 and 99),
    duration_seconds integer not null default 0
        check (duration_seconds between 0 and 86400),
    game_version text not null default 'desconocida'
        check (char_length(game_version) <= 32),
    started_at timestamptz not null,
    ended_at timestamptz not null default now(),
    created_at timestamptz not null default now(),

    constraint partida_con_final_coherente
        check (ended_at >= started_at),

    -- El match-3 no cuenta lineas: si llegan, es que el cliente esta mandando
    -- el resultado del modo equivocado.
    constraint lineas_solo_en_tetris
        check (mode = 'tetris' or lines = 0),

    -- Cota de cordura contra el envio inventado. No es antitrampas de verdad
    -- (eso es la validacion en servidor), pero corta lo mas burdo: un millon
    -- de puntos en cuatro segundos. Si alguna partida legitima la toca, se
    -- sube el factor en vez de quitar la regla.
    constraint puntuacion_al_ritmo_del_reloj
        check (duration_seconds = 0 or score <= 2000 * duration_seconds)
);

-- El indice que sostiene el ranking: mismo orden que la consulta de abajo.
create index if not exists game_sessions_ranking_idx
    on public.game_sessions (mode, score desc, lines desc, ended_at asc);

create index if not exists game_sessions_player_idx
    on public.game_sessions (player_id, ended_at desc);


-- --- Seguridad por fila -----------------------------------------------------
-- Con RLS encendida y sin politica, nadie ve nada. A partir de ahi se abre
-- solo lo justo: cada jugador toca lo suyo y nada mas. El ranking publico no
-- sale de aqui, sale de la funcion leaderboard().

alter table public.players enable row level security;
alter table public.game_sessions enable row level security;

drop policy if exists "el jugador ve su ficha" on public.players;
create policy "el jugador ve su ficha"
    on public.players for select
    using (auth.uid() = id);

drop policy if exists "el jugador crea su ficha" on public.players;
create policy "el jugador crea su ficha"
    on public.players for insert
    with check (auth.uid() = id);

drop policy if exists "el jugador cambia su nombre" on public.players;
create policy "el jugador cambia su nombre"
    on public.players for update
    using (auth.uid() = id)
    with check (auth.uid() = id);

drop policy if exists "el jugador ve sus partidas" on public.game_sessions;
create policy "el jugador ve sus partidas"
    on public.game_sessions for select
    using (auth.uid() = player_id);

drop policy if exists "el jugador apunta sus partidas" on public.game_sessions;
create policy "el jugador apunta sus partidas"
    on public.game_sessions for insert
    with check (auth.uid() = player_id);

-- A proposito no hay politica de UPDATE ni de DELETE sobre game_sessions.


-- --- Alta del jugador -------------------------------------------------------
-- Una llamada y listo: crea la ficha la primera vez y renombra las siguientes.
-- Va como security invoker (lo normal), asi que las politicas de arriba siguen
-- mandando: solo puede escribir sobre su propio id.

create or replace function public.ensure_player(p_display_name text)
returns public.players
language plpgsql
as $$
declare
    resultado public.players;
begin
    if auth.uid() is null then
        raise exception 'hace falta iniciar sesion, aunque sea anonima';
    end if;

    insert into public.players (id, display_name)
    values (auth.uid(), btrim(p_display_name))
    on conflict (id) do update
        set display_name = excluded.display_name
    returning * into resultado;

    return resultado;
end;
$$;


-- --- El ranking -------------------------------------------------------------
-- Security definer porque tiene que leer por encima de RLS: un jugador no ve
-- las partidas de los demas, pero si su puesto en la tabla. Devuelve solo lo
-- que es publico (nombre y marcas); ni el id del jugador ni el correo salen
-- de aqui.
--
-- Una fila por jugador, su mejor partida. Sin el DISTINCT ON, el que mas juega
-- llena el top 10 el solo y la tabla deja de decir nada.

create or replace function public.leaderboard(
    p_mode public.game_mode,
    p_limit integer default 10
)
returns table (
    rank bigint,
    display_name text,
    score integer,
    lines integer,
    level integer,
    ended_at timestamptz
)
language sql
stable
security definer
set search_path = public
as $$
    with mejor_por_jugador as (
        select distinct on (s.player_id)
            s.player_id,
            s.score,
            s.lines,
            s.level,
            s.ended_at
        from public.game_sessions s
        where s.mode = p_mode
        order by s.player_id, s.score desc, s.lines desc, s.ended_at asc
    )
    select
        row_number() over (
            order by m.score desc, m.lines desc, m.ended_at asc
        ) as rank,
        p.display_name,
        m.score,
        m.lines,
        m.level,
        m.ended_at
    from mejor_por_jugador m
    join public.players p on p.id = m.player_id
    order by m.score desc, m.lines desc, m.ended_at asc
    limit least(greatest(coalesce(p_limit, 10), 1), 100);
$$;

-- El ranking se ve sin haber jugado, asi que tambien para el rol anonimo.
grant execute on function public.leaderboard(public.game_mode, integer)
    to anon, authenticated;
grant execute on function public.ensure_player(text) to authenticated;
