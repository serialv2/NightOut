-- Spotiz BLE beacons / automatic verified check-ins.
-- Apply this after checking that public.checkins has the columns used below.

create table if not exists public.bar_beacons (
    id uuid primary key default gen_random_uuid(),
    bar_id uuid not null references public.bars(id) on delete cascade,
    uuid text not null,
    major integer not null,
    minor integer not null,
    label text,
    min_rssi integer not null default -78,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (uuid, major, minor)
);

create index if not exists idx_bar_beacons_bar_id
    on public.bar_beacons(bar_id);

create index if not exists idx_bar_beacons_lookup
    on public.bar_beacons(upper(uuid), major, minor)
    where is_active;

create table if not exists public.beacon_checkin_events (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    bar_id uuid not null references public.bars(id) on delete cascade,
    beacon_id uuid not null references public.bar_beacons(id) on delete cascade,
    checkin_id uuid references public.checkins(id) on delete set null,
    rssi integer not null,
    created_at timestamptz not null default now()
);

create index if not exists idx_beacon_checkin_events_user_created
    on public.beacon_checkin_events(user_id, created_at desc);

alter table public.bar_beacons enable row level security;
alter table public.beacon_checkin_events enable row level security;

drop policy if exists "bar_beacons_read_active" on public.bar_beacons;
create policy "bar_beacons_read_active"
on public.bar_beacons
for select
to authenticated
using (is_active = true);

drop policy if exists "beacon_checkin_events_own_read" on public.beacon_checkin_events;
create policy "beacon_checkin_events_own_read"
on public.beacon_checkin_events
for select
to authenticated
using (user_id = auth.uid());

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
    where upper(uuid) = upper(trim(p_uuid))
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
        now() + interval '1 hour'
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

-- Test beacon received on 2026-07-01:
-- UUID  = 8F2A6B7C-9D31-4E42-AF8B-4D7C2E9F5A10
-- Major = 59001
-- Minor = 1
--
-- Example link to a bar:
-- insert into public.bar_beacons (bar_id, uuid, major, minor, label, min_rssi)
-- values (
--   '<BAR_UUID_HERE>',
--   '8F2A6B7C-9D31-4E42-AF8B-4D7C2E9F5A10',
--   59001,
--   1,
--   'Spotiz test beacon 001',
--   -78
-- );

-- Assign the received beacon to the bar "La Maison".
-- Safe to run multiple times: it updates the existing beacon row if it already exists.
do $$
declare
    v_bar_id uuid;
begin
    select id
    into v_bar_id
    from public.bars
    where lower(name) = lower('La Maison')
       or name ilike 'La Maison%'
    order by
        case when lower(name) = lower('La Maison') then 0 else 1 end,
        name
    limit 1;

    if v_bar_id is null then
        raise exception 'Bar "La Maison" introuvable dans public.bars';
    end if;

    insert into public.bar_beacons (bar_id, uuid, major, minor, label, min_rssi, is_active)
    values (
        v_bar_id,
        '8F2A6B7C-9D31-4E42-AF8B-4D7C2E9F5A10',
        59001,
        1,
        'Spotiz beacon - La Maison',
        -78,
        true
    )
    on conflict (uuid, major, minor)
    do update set
        bar_id = excluded.bar_id,
        label = excluded.label,
        min_rssi = excluded.min_rssi,
        is_active = true,
        updated_at = now();
end;
$$;
