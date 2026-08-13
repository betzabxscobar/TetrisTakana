-- =====================================================================
-- TetrisTakana - Esquema de la base de datos (Supabase / PostgreSQL)
-- =====================================================================
--
-- ATENCION: este archivo esta RECONSTRUIDO a partir de lo que pide el
-- cliente del juego (Assets/Scripts/Online/SupabaseClient.cs), no volcado
-- del proyecto de Supabase que esta en produccion. Sirve para dos cosas:
--
--   1. Documentar el contrato: que tablas y funciones necesita el juego
--      para funcionar, con sus columnas y sus tipos.
--   2. Levantar una base nueva desde cero (otro proyecto de Supabase, o
--      un PostgreSQL local) y que el juego funcione contra ella.
--
-- Para entregar el volcado DE VERDAD, el que incluye los datos y
-- cualquier cambio hecho a mano desde el panel:
--
--   Panel de Supabase > SQL Editor, y ejecutar cada consulta; o bien,
--   con la CLI instalada y sesion iniciada:
--
--     supabase link --project-ref gavlmybnbfwlvgtoeijy
--     supabase db dump -f Docs/BaseDeDatos.sql          (solo estructura)
--     supabase db dump -f Docs/Datos.sql --data-only    (solo datos)
--
-- El proyecto en produccion es https://gavlmybnbfwlvgtoeijy.supabase.co
--
-- =====================================================================


-- ---------------------------------------------------------------------
-- Jugadores
-- ---------------------------------------------------------------------
-- El juego no pide correo ni contrasena: entra con un usuario anonimo de
-- Supabase Auth (auth/v1/signup con cuerpo vacio) y guarda el token de
-- refresco en PlayerPrefs. Por eso la fila de jugador cuelga de
-- auth.users: el id es el mismo.
create table if not exists public.players (
    id           uuid        primary key references auth.users (id) on delete cascade,
    display_name text        not null check (char_length(display_name) between 1 and 16),
    created_at   timestamptz not null default now(),
    updated_at   timestamptz not null default now()
);

-- El limite de 16 no es decorativo: NamePrompt.MaxLength recorta el
-- nombre a 16 antes de enviarlo, y si aqui cupieran mas los dos numeros
-- dejarian de cuadrar.


-- ---------------------------------------------------------------------
-- Partidas
-- ---------------------------------------------------------------------
-- Una fila por partida terminada. El juego las inserta con
-- Prefer: return=minimal, asi que no necesita que la insercion devuelva
-- nada.
create table if not exists public.game_sessions (
    id               bigint      generated always as identity primary key,
    player_id        uuid        not null references public.players (id) on delete cascade,
    mode             text        not null check (mode in ('tetris', 'match3')),
    score            integer     not null check (score >= 0),
    lines            integer     not null default 0 check (lines >= 0),
    level            integer     not null default 1 check (level >= 1),
    duration_seconds integer     not null check (duration_seconds >= 0),
    game_version     text        not null default 'desconocida',
    started_at       timestamptz not null,
    ended_at         timestamptz not null,
    created_at       timestamptz not null default now(),

    -- Una partida no puede acabar antes de empezar.
    constraint game_sessions_orden_fechas check (ended_at >= started_at)
);

-- El ranking siempre pide "las mejores de un modo", asi que el indice va
-- por modo y puntuacion descendente; sin el, cada consulta se lee la
-- tabla entera.
create index if not exists game_sessions_ranking_idx
    on public.game_sessions (mode, score desc, ended_at);

create index if not exists game_sessions_player_idx
    on public.game_sessions (player_id);


-- ---------------------------------------------------------------------
-- Seguridad a nivel de fila
-- ---------------------------------------------------------------------
-- La clave que viaja dentro del build es la publishable: la puede leer
-- cualquiera que descargue el juego. Lo que impide que alguien borre la
-- tabla o inserte puntuaciones a nombre de otro son estas politicas, no
-- la clave. Si se desactiva RLS, el ranking queda abierto de par en par.
alter table public.players       enable row level security;
alter table public.game_sessions enable row level security;

-- Cada quien ve y toca solo su propia fila de jugador.
create policy players_leer_lo_suyo on public.players
    for select using (auth.uid() = id);

create policy players_editar_lo_suyo on public.players
    for update using (auth.uid() = id) with check (auth.uid() = id);

-- Las partidas se insertan solo a nombre propio. No hay politica de
-- update ni de delete a proposito: una partida enviada no se retoca.
create policy sesiones_insertar_lo_suyo on public.game_sessions
    for insert with check (auth.uid() = player_id);

create policy sesiones_leer_lo_suyo on public.game_sessions
    for select using (auth.uid() = player_id);

-- El ranking global se lee por la funcion leaderboard(), que es
-- security definer: por eso no hace falta abrir la tabla a todo el mundo.


-- ---------------------------------------------------------------------
-- ensure_player(p_display_name)
-- ---------------------------------------------------------------------
-- La llama el juego cuando el jugador escribe su nombre. Da de alta la
-- fila si no existe y le cambia el nombre si ya estaba.
create or replace function public.ensure_player(p_display_name text)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
    v_id     uuid := auth.uid();
    v_nombre text := btrim(p_display_name);
begin
    if v_id is null then
        raise exception 'Hace falta una sesion iniciada.';
    end if;

    -- Se recorta aqui tambien y no solo en el cliente: por REST puede
    -- llamar cualquiera, y el check de la tabla rechazaria la fila.
    if char_length(v_nombre) = 0 then
        raise exception 'El nombre no puede estar vacio.';
    end if;

    v_nombre := left(v_nombre, 16);

    insert into public.players (id, display_name)
    values (v_id, v_nombre)
    on conflict (id) do update
        set display_name = excluded.display_name,
            updated_at   = now();
end;
$$;


-- ---------------------------------------------------------------------
-- leaderboard(p_mode, p_limit)
-- ---------------------------------------------------------------------
-- Las mejores partidas de un modo, una por jugador. Es security definer
-- porque tiene que ver las partidas de todos, que es justo lo que las
-- politicas de arriba no dejan hacer directamente.
create or replace function public.leaderboard(p_mode text, p_limit integer)
returns table (
    rank         integer,
    display_name text,
    score        integer,
    lines        integer,
    level        integer,
    ended_at     timestamptz
)
language sql
security definer
set search_path = public
as $$
    with mejores as (
        -- distinct on se queda con la primera fila de cada jugador segun
        -- el order by, o sea con su mejor partida del modo pedido.
        select distinct on (s.player_id)
               s.player_id,
               s.score,
               s.lines,
               s.level,
               s.ended_at
        from public.game_sessions s
        where s.mode = p_mode
        order by s.player_id, s.score desc, s.ended_at asc
    )
    select row_number() over (order by m.score desc, m.ended_at asc)::integer,
           p.display_name,
           m.score,
           m.lines,
           m.level,
           m.ended_at
    from mejores m
    join public.players p on p.id = m.player_id
    order by m.score desc, m.ended_at asc
    limit greatest(1, least(coalesce(p_limit, 10), 100));
$$;

-- El cliente manda p_limit entre 1 y 100; el least/greatest esta aqui
-- por si la llamada llega por otro lado con un numero absurdo.

grant execute on function public.ensure_player(text)          to authenticated;
grant execute on function public.leaderboard(text, integer)   to anon, authenticated;

-- leaderboard se concede tambien a anon porque la pantalla de
-- puntuaciones se abre desde el menu, antes de que el jugador haya
-- entrado en ninguna partida.
