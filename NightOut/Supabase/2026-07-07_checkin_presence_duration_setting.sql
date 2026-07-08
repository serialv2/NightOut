-- Reglage global de duree de presence check-in.
-- A executer dans Supabase SQL Editor.

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

insert into public.app_settings (
    setting_key,
    setting_value,
    label,
    description
)
values (
    'checkin_presence_duration_minutes',
    '60',
    'Duree de presence check-in',
    'Duree pendant laquelle un utilisateur reste present dans un bar apres check-in, sauf check-out manuel ou heartbeat.'
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

-- Heartbeat mobile : prolonge la presence selon le reglage admin.
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
end;
$$;

grant execute on function public.heartbeat_presence()
to authenticated;

-- Version beacon de la RPC : utilise la duree configuree au lieu de "1 hour".
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
        raise exception 'beacon_inconnu';
    end if;

    if p_rssi < v_beacon.min_rssi then
        raise exception 'signal_trop_faible';
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

-- A appliquer aussi dans la RPC public.check_in si elle existe deja dans Supabase :
-- remplacer "now() + interval '1 hour'" par "now() + public.checkin_presence_duration()".
