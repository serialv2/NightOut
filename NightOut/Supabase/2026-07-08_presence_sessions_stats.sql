-- Spotiz - Presence sessions, activity journal and bar statistics.
-- A executer dans Supabase SQL Editor.
--
-- Objectif:
-- - 1 utilisateur = 1 seule presence active a la fois.
-- - Historique propre des visites avec duree.
-- - Journal utilisateur separe du fil d'activite des bars.
-- - Stats exploitables par les bars et par l'admin.

create extension if not exists pgcrypto;

create table if not exists public.app_settings (
    id uuid primary key default gen_random_uuid(),
    setting_key text not null unique,
    setting_value text not null,
    label text not null,
    description text,
    updated_at timestamptz not null default now()
);

alter table public.app_settings enable row level security;

drop policy if exists "app_settings_admin_all" on public.app_settings;
create policy "app_settings_admin_all"
on public.app_settings
for all
to authenticated
using (
    exists (
        select 1
        from public.profiles p
        where p.id = auth.uid()
          and coalesce(p.is_admin, false) = true
    )
)
with check (
    exists (
        select 1
        from public.profiles p
        where p.id = auth.uid()
          and coalesce(p.is_admin, false) = true
    )
);

drop policy if exists "app_settings_read_authenticated" on public.app_settings;
create policy "app_settings_read_authenticated"
on public.app_settings
for select
to authenticated
using (true);

create or replace function public.is_admin()
returns boolean
language sql
stable
security definer
set search_path = public
as $$
    select exists (
        select 1
        from public.profiles p
        where p.id = auth.uid()
          and coalesce(p.is_admin, false) = true
    );
$$;

create or replace function public.can_access_bar_stats(p_bar_id uuid)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
    select coalesce(public.is_admin(), false)
        or exists (
            select 1
            from public.bars b
            left join public.professional_accounts pa
              on pa.id = b.professional_account_id
            where b.id = p_bar_id
              and (
                  b.owner_id = auth.uid()
                  or pa.user_id = auth.uid()
              )
        );
$$;

insert into public.app_settings (
    setting_key,
    setting_value,
    label,
    description
)
values
(
    'checkin_presence_duration_minutes',
    '60',
    'Duree de presence check-in',
    'Duree visible de presence apres check-in, prolongee par heartbeat.'
),
(
    'auto_checkout_after_inactive_minutes',
    '15',
    'Auto checkout app inactive',
    'Cloture une presence active si aucun heartbeat ou scan beacon/GPS ne confirme la presence.'
)
on conflict (setting_key)
do update set
    label = excluded.label,
    description = excluded.description,
    updated_at = now();

create or replace function public.get_app_setting_int(
    p_key text,
    p_default integer,
    p_min integer default null,
    p_max integer default null
)
returns integer
language plpgsql
stable
set search_path = public
as $$
declare
    v_raw text;
    v_value integer;
begin
    select setting_value
    into v_raw
    from public.app_settings
    where setting_key = p_key
    limit 1;

    begin
        v_value := coalesce(nullif(trim(v_raw), '')::integer, p_default);
    exception when others then
        v_value := p_default;
    end;

    if p_min is not null then
        v_value := greatest(v_value, p_min);
    end if;

    if p_max is not null then
        v_value := least(v_value, p_max);
    end if;

    return v_value;
end;
$$;

create or replace function public.checkin_presence_duration()
returns interval
language sql
stable
set search_path = public
as $$
    select make_interval(
        mins => public.get_app_setting_int(
            'checkin_presence_duration_minutes',
            60,
            5,
            1440
        )
    );
$$;

create table if not exists public.presence_sessions (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    bar_id uuid not null references public.bars(id) on delete cascade,
    checkin_id uuid references public.checkins(id) on delete set null,
    started_at timestamptz not null default now(),
    ended_at timestamptz,
    last_seen_at timestamptz not null default now(),
    duration_seconds integer not null default 0,
    checkin_source text not null default 'manual',
    checkout_source text,
    status text not null default 'active',
    metadata jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint presence_sessions_status_check
        check (status in ('active', 'closed', 'expired')),
    constraint presence_sessions_checkin_source_check
        check (checkin_source in ('manual', 'gps', 'beacon', 'admin', 'import')),
    constraint presence_sessions_checkout_source_check
        check (
            checkout_source is null
            or checkout_source in ('manual', 'gps', 'beacon', 'app_inactive', 'switch_bar', 'admin', 'server_expired')
        )
);

with ranked_checkins as (
    select
        c.*,
        row_number() over (
            partition by c.user_id
            order by
                case when coalesce(c.is_active, false) then 0 else 1 end,
                c.checked_in_at desc
        ) as user_rank
    from public.checkins c
)
insert into public.presence_sessions (
    user_id,
    bar_id,
    checkin_id,
    started_at,
    ended_at,
    last_seen_at,
    duration_seconds,
    checkin_source,
    checkout_source,
    status,
    metadata
)
select
    c.user_id,
    c.bar_id,
    c.id,
    c.checked_in_at,
    case
        when coalesce(c.is_active, false) and c.user_rank = 1 then null
        else coalesce(c.checked_out_at, now())
    end,
    case
        when coalesce(c.is_active, false) and c.user_rank = 1 then now()
        else coalesce(c.checked_out_at, c.checked_in_at, now())
    end,
    case
        when coalesce(c.is_active, false) and c.user_rank = 1 then 0
        else greatest(0, extract(epoch from (coalesce(c.checked_out_at, now()) - c.checked_in_at))::integer)
    end,
    'import',
    case
        when coalesce(c.is_active, false) and c.user_rank = 1 then null
        else 'admin'
    end,
    case
        when coalesce(c.is_active, false) and c.user_rank = 1 then 'active'
        else 'closed'
    end,
    jsonb_build_object('backfilled_from_checkins', true)
from ranked_checkins c
where c.checked_in_at is not null
  and not exists (
      select 1
      from public.presence_sessions ps
      where ps.checkin_id = c.id
  );

with active_ranked_checkins as (
    select
        id,
        row_number() over (
            partition by user_id
            order by checked_in_at desc
        ) as active_rank
    from public.checkins
    where is_active = true
)
update public.checkins c
set is_active = false,
    checked_out_at = coalesce(c.checked_out_at, now())
from active_ranked_checkins r
where c.id = r.id
  and r.active_rank > 1;

create unique index if not exists idx_presence_sessions_one_active_user
    on public.presence_sessions(user_id)
    where status = 'active' and ended_at is null;

create index if not exists idx_presence_sessions_bar_started
    on public.presence_sessions(bar_id, started_at desc);

create index if not exists idx_presence_sessions_user_started
    on public.presence_sessions(user_id, started_at desc);

create index if not exists idx_presence_sessions_active_last_seen
    on public.presence_sessions(last_seen_at)
    where status = 'active' and ended_at is null;

create table if not exists public.user_activity_log (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    bar_id uuid references public.bars(id) on delete set null,
    presence_session_id uuid references public.presence_sessions(id) on delete set null,
    checkin_id uuid references public.checkins(id) on delete set null,
    activity_type text not null,
    source text,
    metadata jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    constraint user_activity_log_type_check
        check (activity_type in (
            'checkin',
            'checkout',
            'switch_bar',
            'presence_expired',
            'heartbeat',
            'reward_earned',
            'reward_redeemed',
            'event_joined',
            'beacon_rejected',
            'admin_note'
        ))
);

create index if not exists idx_user_activity_log_user_created
    on public.user_activity_log(user_id, created_at desc);

create index if not exists idx_user_activity_log_bar_created
    on public.user_activity_log(bar_id, created_at desc);

alter table public.presence_sessions enable row level security;
alter table public.user_activity_log enable row level security;

drop policy if exists "presence_sessions_user_read" on public.presence_sessions;
create policy "presence_sessions_user_read"
on public.presence_sessions
for select
to authenticated
using (
    user_id = auth.uid()
    or public.can_access_bar_stats(bar_id)
);

drop policy if exists "presence_sessions_admin_all" on public.presence_sessions;
create policy "presence_sessions_admin_all"
on public.presence_sessions
for all
to authenticated
using (public.is_admin())
with check (public.is_admin());

drop policy if exists "user_activity_log_user_read" on public.user_activity_log;
create policy "user_activity_log_user_read"
on public.user_activity_log
for select
to authenticated
using (
    user_id = auth.uid()
    or (bar_id is not null and public.can_access_bar_stats(bar_id))
);

drop policy if exists "user_activity_log_admin_all" on public.user_activity_log;
create policy "user_activity_log_admin_all"
on public.user_activity_log
for all
to authenticated
using (public.is_admin())
with check (public.is_admin());

create or replace function public.close_active_presence_session(
    p_user_id uuid,
    p_checkout_source text default 'manual',
    p_reason text default null,
    p_ended_at timestamptz default now()
)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
    v_session public.presence_sessions%rowtype;
    v_status text;
begin
    select *
    into v_session
    from public.presence_sessions
    where user_id = p_user_id
      and status = 'active'
      and ended_at is null
    order by started_at desc
    limit 1
    for update;

    if not found then
        return null;
    end if;

    v_status := case
        when p_checkout_source in ('app_inactive', 'server_expired') then 'expired'
        else 'closed'
    end;

    update public.presence_sessions
    set ended_at = p_ended_at,
        last_seen_at = greatest(last_seen_at, p_ended_at),
        duration_seconds = greatest(0, extract(epoch from (p_ended_at - started_at))::integer),
        checkout_source = p_checkout_source,
        status = v_status,
        updated_at = now(),
        metadata = metadata || jsonb_build_object('checkout_reason', coalesce(p_reason, p_checkout_source))
    where id = v_session.id;

    update public.checkins
    set is_active = false,
        checked_out_at = coalesce(checked_out_at, p_ended_at)
    where user_id = p_user_id
      and is_active = true
      and (
          v_session.checkin_id is null
          or id = v_session.checkin_id
      );

    insert into public.user_activity_log (
        user_id,
        bar_id,
        presence_session_id,
        checkin_id,
        activity_type,
        source,
        metadata,
        created_at
    )
    values (
        v_session.user_id,
        v_session.bar_id,
        v_session.id,
        v_session.checkin_id,
        case when v_status = 'expired' then 'presence_expired' else 'checkout' end,
        p_checkout_source,
        jsonb_build_object(
            'reason', coalesce(p_reason, p_checkout_source),
            'duration_seconds', greatest(0, extract(epoch from (p_ended_at - v_session.started_at))::integer)
        ),
        p_ended_at
    );

    return v_session.id;
end;
$$;

create or replace function public.open_presence_session(
    p_user_id uuid,
    p_bar_id uuid,
    p_checkin_id uuid,
    p_source text default 'manual',
    p_metadata jsonb default '{}'::jsonb
)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
    v_existing public.presence_sessions%rowtype;
    v_session_id uuid;
begin
    select *
    into v_existing
    from public.presence_sessions
    where user_id = p_user_id
      and status = 'active'
      and ended_at is null
    order by started_at desc
    limit 1
    for update;

    if found and v_existing.bar_id <> p_bar_id then
        perform public.close_active_presence_session(
            p_user_id,
            'switch_bar',
            'checkin_new_bar',
            now()
        );

        insert into public.user_activity_log (
            user_id,
            bar_id,
            presence_session_id,
            checkin_id,
            activity_type,
            source,
            metadata
        )
        values (
            p_user_id,
            p_bar_id,
            null,
            p_checkin_id,
            'switch_bar',
            p_source,
            jsonb_build_object('from_bar_id', v_existing.bar_id, 'to_bar_id', p_bar_id)
        );
    elsif found and v_existing.bar_id = p_bar_id then
        update public.presence_sessions
        set last_seen_at = now(),
            checkin_id = coalesce(checkin_id, p_checkin_id),
            metadata = metadata || p_metadata,
            updated_at = now()
        where id = v_existing.id;

        return v_existing.id;
    end if;

    insert into public.presence_sessions (
        user_id,
        bar_id,
        checkin_id,
        started_at,
        last_seen_at,
        checkin_source,
        metadata
    )
    values (
        p_user_id,
        p_bar_id,
        p_checkin_id,
        now(),
        now(),
        p_source,
        p_metadata
    )
    returning id into v_session_id;

    insert into public.user_activity_log (
        user_id,
        bar_id,
        presence_session_id,
        checkin_id,
        activity_type,
        source,
        metadata
    )
    values (
        p_user_id,
        p_bar_id,
        v_session_id,
        p_checkin_id,
        'checkin',
        p_source,
        p_metadata
    );

    return v_session_id;
end;
$$;

drop function if exists public.check_in(
    uuid,
    uuid,
    double precision,
    double precision,
    uuid
);

create or replace function public.check_in(
    p_bar_id uuid,
    p_user_id uuid,
    p_lat double precision,
    p_lng double precision,
    p_event_id uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_bar public.bars%rowtype;
    v_checkin public.checkins%rowtype;
    v_distance_m double precision;
    v_radius_m double precision;
    v_source text := case when p_lat is null or p_lng is null then 'manual' else 'gps' end;
begin
    if auth.uid() is null or auth.uid() <> p_user_id then
        raise exception 'not_allowed';
    end if;

    select *
    into v_bar
    from public.bars
    where id = p_bar_id
      and coalesce(is_active, true) = true
    limit 1;

    if not found then
        raise exception 'bar_inconnu';
    end if;

    if p_lat is not null and p_lng is not null then
        v_radius_m := greatest(coalesce(v_bar.radius_m, 100), 30);

        v_distance_m :=
            6371000 * 2 * asin(
                sqrt(
                    pow(sin(radians((v_bar.latitude - p_lat) / 2)), 2) +
                    cos(radians(p_lat)) *
                    cos(radians(v_bar.latitude)) *
                    pow(sin(radians((v_bar.longitude - p_lng) / 2)), 2)
                )
            );

        if v_distance_m > v_radius_m then
            raise exception 'trop_loin';
        end if;
    end if;

    if exists (
        select 1
        from public.presence_sessions
        where user_id = p_user_id
          and status = 'active'
          and ended_at is null
          and bar_id <> p_bar_id
    ) then
        perform public.close_active_presence_session(
            p_user_id,
            'switch_bar',
            'checkin_new_bar',
            now()
        );
    end if;

    update public.checkins
    set is_active = false,
        checked_out_at = coalesce(checked_out_at, now())
    where user_id = p_user_id
      and is_active = true
      and bar_id <> p_bar_id;

    select *
    into v_checkin
    from public.checkins
    where user_id = p_user_id
      and bar_id = p_bar_id
      and is_active = true
    order by checked_in_at desc
    limit 1;

    if not found then
        insert into public.checkins (
            id,
            user_id,
            bar_id,
            event_id,
            checked_in_at,
            is_active
        )
        values (
            gen_random_uuid(),
            p_user_id,
            p_bar_id,
            nullif(p_event_id, '00000000-0000-0000-0000-000000000000'::uuid),
            now(),
            true
        )
        returning * into v_checkin;
    end if;

    perform public.open_presence_session(
        p_user_id,
        p_bar_id,
        v_checkin.id,
        v_source,
        jsonb_build_object('latitude', p_lat, 'longitude', p_lng)
    );

    insert into public.user_statuses (
        user_id,
        status,
        bar_id,
        updated_at,
        expires_at
    )
    values (
        p_user_id,
        'out',
        p_bar_id,
        now(),
        now() + public.checkin_presence_duration()
    )
    on conflict (user_id)
    do update set
        status = excluded.status,
        bar_id = excluded.bar_id,
        updated_at = excluded.updated_at,
        expires_at = excluded.expires_at;

    return to_jsonb(v_checkin);
end;
$$;

grant execute on function public.check_in(uuid, uuid, double precision, double precision, uuid)
to authenticated;

create or replace function public.check_in_by_beacon(
    p_user_id uuid,
    p_uuid text,
    p_major integer,
    p_minor integer,
    p_rssi integer
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_beacon public.bar_beacons%rowtype;
    v_checkin public.checkins%rowtype;
begin
    if auth.uid() is null or auth.uid() <> p_user_id then
        raise exception 'not_allowed';
    end if;

    select *
    into v_beacon
    from public.bar_beacons
    where upper(coalesce(beacon_uuid::text, uuid)) = upper(trim(p_uuid))
      and major = p_major
      and minor = p_minor
      and is_active = true
    limit 1;

    if not found then
        insert into public.user_activity_log (
            user_id,
            activity_type,
            source,
            metadata
        )
        values (
            p_user_id,
            'beacon_rejected',
            'beacon',
            jsonb_build_object('uuid', p_uuid, 'major', p_major, 'minor', p_minor, 'rssi', p_rssi, 'reason', 'beacon_inconnu')
        );
        raise exception 'beacon_inconnu';
    end if;

    if p_rssi < v_beacon.min_rssi then
        insert into public.user_activity_log (
            user_id,
            bar_id,
            activity_type,
            source,
            metadata
        )
        values (
            p_user_id,
            v_beacon.bar_id,
            'beacon_rejected',
            'beacon',
            jsonb_build_object('uuid', p_uuid, 'major', p_major, 'minor', p_minor, 'rssi', p_rssi, 'min_rssi', v_beacon.min_rssi, 'reason', 'signal_trop_faible')
        );
        raise exception 'signal_trop_faible';
    end if;

    if exists (
        select 1
        from public.presence_sessions
        where user_id = p_user_id
          and status = 'active'
          and ended_at is null
          and bar_id <> v_beacon.bar_id
    ) then
        perform public.close_active_presence_session(
            p_user_id,
            'switch_bar',
            'beacon_new_bar',
            now()
        );
    end if;

    update public.checkins
    set is_active = false,
        checked_out_at = now()
    where user_id = p_user_id
      and is_active = true
      and bar_id <> v_beacon.bar_id;

    select *
    into v_checkin
    from public.checkins
    where user_id = p_user_id
      and bar_id = v_beacon.bar_id
      and is_active = true
    order by checked_in_at desc
    limit 1;

    if not found then
        insert into public.checkins (
            id,
            user_id,
            bar_id,
            checked_in_at,
            is_active
        )
        values (
            gen_random_uuid(),
            p_user_id,
            v_beacon.bar_id,
            now(),
            true
        )
        returning * into v_checkin;
    end if;

    perform public.open_presence_session(
        p_user_id,
        v_beacon.bar_id,
        v_checkin.id,
        'beacon',
        jsonb_build_object(
            'beacon_id', v_beacon.id,
            'uuid', p_uuid,
            'major', p_major,
            'minor', p_minor,
            'rssi', p_rssi
        )
    );

    insert into public.beacon_checkin_events (
        user_id,
        bar_id,
        beacon_id,
        checkin_id,
        rssi
    )
    values (
        p_user_id,
        v_beacon.bar_id,
        v_beacon.id,
        v_checkin.id,
        p_rssi
    );

    insert into public.user_statuses (
        user_id,
        status,
        bar_id,
        updated_at,
        expires_at
    )
    values (
        p_user_id,
        'out',
        v_beacon.bar_id,
        now(),
        now() + public.checkin_presence_duration()
    )
    on conflict (user_id)
    do update set
        status = excluded.status,
        bar_id = excluded.bar_id,
        updated_at = excluded.updated_at,
        expires_at = excluded.expires_at;

    return to_jsonb(v_checkin);
end;
$$;

grant execute on function public.check_in_by_beacon(uuid, text, integer, integer, integer)
to authenticated;

create or replace function public.check_out(
    p_checkin_id uuid,
    p_checkout_source text default 'manual'
)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
declare
    v_checkin public.checkins%rowtype;
begin
    if auth.uid() is null then
        raise exception 'not_allowed';
    end if;

    select *
    into v_checkin
    from public.checkins
    where id = p_checkin_id
      and user_id = auth.uid()
    limit 1;

    if not found then
        raise exception 'checkin_introuvable';
    end if;

    perform public.close_active_presence_session(
        auth.uid(),
        p_checkout_source,
        'manual_checkout',
        now()
    );

    update public.checkins
    set is_active = false,
        checked_out_at = coalesce(checked_out_at, now())
    where id = p_checkin_id
      and user_id = auth.uid();

    update public.user_statuses
    set status = 'online',
        bar_id = null,
        updated_at = now(),
        expires_at = now() + interval '15 minutes'
    where user_id = auth.uid();

    return true;
end;
$$;

grant execute on function public.check_out(uuid, text)
to authenticated;

create or replace function public.check_out_active(
    p_checkout_source text default 'manual'
)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
begin
    if auth.uid() is null then
        raise exception 'not_allowed';
    end if;

    perform public.close_active_presence_session(
        auth.uid(),
        p_checkout_source,
        'active_checkout',
        now()
    );

    update public.checkins
    set is_active = false,
        checked_out_at = coalesce(checked_out_at, now())
    where user_id = auth.uid()
      and is_active = true;

    update public.user_statuses
    set status = 'online',
        bar_id = null,
        updated_at = now(),
        expires_at = now() + interval '15 minutes'
    where user_id = auth.uid();

    return true;
end;
$$;

grant execute on function public.check_out_active(text)
to authenticated;

create or replace function public.heartbeat_presence()
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if auth.uid() is null then
        raise exception 'not_allowed';
    end if;

    update public.user_statuses
    set updated_at = now(),
        expires_at = now() + public.checkin_presence_duration()
    where user_id = auth.uid()
      and status <> 'offline';

    update public.presence_sessions
    set last_seen_at = now(),
        updated_at = now()
    where user_id = auth.uid()
      and status = 'active'
      and ended_at is null;
end;
$$;

grant execute on function public.heartbeat_presence()
to authenticated;

create or replace function public.expire_stale_presence_sessions()
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
    v_cutoff timestamptz;
    v_session record;
    v_count integer := 0;
begin
    v_cutoff := now() - make_interval(
        mins => public.get_app_setting_int(
            'auto_checkout_after_inactive_minutes',
            15,
            3,
            240
        )
    );

    for v_session in
        select id, user_id, last_seen_at
        from public.presence_sessions
        where status = 'active'
          and ended_at is null
          and last_seen_at < v_cutoff
        order by last_seen_at asc
    loop
        perform public.close_active_presence_session(
            v_session.user_id,
            'app_inactive',
            'heartbeat_timeout',
            v_session.last_seen_at
                + make_interval(
                    mins => public.get_app_setting_int(
                        'auto_checkout_after_inactive_minutes',
                        15,
                        3,
                        240
                    )
                )
        );

        v_count := v_count + 1;
    end loop;

    return v_count;
end;
$$;

grant execute on function public.expire_stale_presence_sessions()
to authenticated;

create or replace function public.sync_presence_session_from_checkin_update()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if old.is_active = true and new.is_active = false then
        update public.presence_sessions
        set ended_at = coalesce(new.checked_out_at, now()),
            duration_seconds = greatest(0, extract(epoch from (coalesce(new.checked_out_at, now()) - started_at))::integer),
            checkout_source = coalesce(checkout_source, 'manual'),
            status = case when status = 'active' then 'closed' else status end,
            updated_at = now()
        where checkin_id = new.id
          and ended_at is null;
    end if;

    return new;
end;
$$;

drop trigger if exists trg_sync_presence_session_from_checkin_update on public.checkins;
create trigger trg_sync_presence_session_from_checkin_update
after update of is_active, checked_out_at on public.checkins
for each row
execute function public.sync_presence_session_from_checkin_update();

create or replace function public.get_bar_presence_stats(
    p_bar_id uuid,
    p_from timestamptz default now() - interval '30 days',
    p_to timestamptz default now()
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_result jsonb;
begin
    if auth.uid() is null or not public.can_access_bar_stats(p_bar_id) then
        raise exception 'not_allowed';
    end if;

    perform public.expire_stale_presence_sessions();

    with sessions as (
        select *
        from public.presence_sessions
        where bar_id = p_bar_id
          and started_at >= p_from
          and started_at < p_to
    ),
    closed_sessions as (
        select *
        from sessions
        where status <> 'active'
          and duration_seconds > 0
    ),
    source_rows as (
        select checkin_source, count(*) as total
        from sessions
        group by checkin_source
    ),
    hourly_rows as (
        select extract(hour from started_at)::integer as hour, count(*) as total
        from sessions
        group by extract(hour from started_at)::integer
    ),
    daily_rows as (
        select date_trunc('day', started_at)::date as day, count(*) as total, count(distinct user_id) as unique_visitors
        from sessions
        group by date_trunc('day', started_at)::date
        order by day
    ),
    repeat_rows as (
        select user_id
        from sessions
        group by user_id
        having count(*) > 1
    )
    select jsonb_build_object(
        'bar_id', p_bar_id,
        'from', p_from,
        'to', p_to,
        'current_present', (
            select count(*)
            from public.presence_sessions
            where bar_id = p_bar_id
              and status = 'active'
              and ended_at is null
        ),
        'visits_total', (select count(*) from sessions),
        'unique_visitors', (select count(distinct user_id) from sessions),
        'repeat_visitors', (select count(*) from repeat_rows),
        'avg_duration_minutes', coalesce((select round(avg(duration_seconds) / 60.0, 1) from closed_sessions), 0),
        'checkin_sources', coalesce((select jsonb_object_agg(checkin_source, total) from source_rows), '{}'::jsonb),
        'hourly', coalesce((select jsonb_agg(jsonb_build_object('hour', hour, 'total', total) order by hour) from hourly_rows), '[]'::jsonb),
        'daily', coalesce((select jsonb_agg(jsonb_build_object('day', day, 'total', total, 'unique_visitors', unique_visitors) order by day) from daily_rows), '[]'::jsonb)
    )
    into v_result;

    return v_result;
end;
$$;

grant execute on function public.get_bar_presence_stats(uuid, timestamptz, timestamptz)
to authenticated;

create or replace function public.get_user_activity_journal(
    p_user_id uuid default auth.uid(),
    p_limit integer default 100
)
returns table (
    id uuid,
    user_id uuid,
    bar_id uuid,
    bar_name text,
    activity_type text,
    source text,
    metadata jsonb,
    created_at timestamptz
)
language plpgsql
security definer
set search_path = public
as $$
begin
    if auth.uid() is null then
        raise exception 'not_allowed';
    end if;

    if p_user_id <> auth.uid() and not public.is_admin() then
        raise exception 'not_allowed';
    end if;

    return query
    select
        l.id,
        l.user_id,
        l.bar_id,
        b.name as bar_name,
        l.activity_type,
        l.source,
        l.metadata,
        l.created_at
    from public.user_activity_log l
    left join public.bars b on b.id = l.bar_id
    where l.user_id = p_user_id
    order by l.created_at desc
    limit greatest(1, least(coalesce(p_limit, 100), 500));
end;
$$;

grant execute on function public.get_user_activity_journal(uuid, integer)
to authenticated;

create or replace function public.get_admin_presence_overview(
    p_from timestamptz default now() - interval '30 days',
    p_to timestamptz default now()
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
begin
    if auth.uid() is null or not public.is_admin() then
        raise exception 'not_allowed';
    end if;

    perform public.expire_stale_presence_sessions();

    return (
        select jsonb_build_object(
            'from', p_from,
            'to', p_to,
            'current_present', (
                select count(*)
                from public.presence_sessions
                where status = 'active'
                  and ended_at is null
            ),
            'visits_total', (
                select count(*)
                from public.presence_sessions
                where started_at >= p_from
                  and started_at < p_to
            ),
            'unique_visitors', (
                select count(distinct user_id)
                from public.presence_sessions
                where started_at >= p_from
                  and started_at < p_to
            ),
            'active_bars', (
                select count(distinct bar_id)
                from public.presence_sessions
                where started_at >= p_from
                  and started_at < p_to
            ),
            'top_bars', coalesce((
                select jsonb_agg(row_to_json(t))
                from (
                    select
                        b.id as bar_id,
                        b.name as bar_name,
                        count(*) as visits_total,
                        count(distinct ps.user_id) as unique_visitors,
                        coalesce(round(avg(nullif(ps.duration_seconds, 0)) / 60.0, 1), 0) as avg_duration_minutes
                    from public.presence_sessions ps
                    join public.bars b on b.id = ps.bar_id
                    where ps.started_at >= p_from
                      and ps.started_at < p_to
                    group by b.id, b.name
                    order by visits_total desc
                    limit 20
                ) t
            ), '[]'::jsonb)
        )
    );
end;
$$;

grant execute on function public.get_admin_presence_overview(timestamptz, timestamptz)
to authenticated;

notify pgrst, 'reload schema';
