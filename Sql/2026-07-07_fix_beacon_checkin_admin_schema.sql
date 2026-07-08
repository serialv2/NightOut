-- Spotiz / NightOut - Compatibilite admin VB.NET + check-in beacon mobile
--
-- Probleme corrige :
-- - l'admin VB.NET enregistre la balise dans public.bar_beacons.beacon_uuid
-- - l'ancienne fonction mobile check_in_by_beacon cherchait public.bar_beacons.uuid
-- Resultat : "beacon_inconnu" / "check-in refuse" apres programmation depuis l'admin.

alter table public.bar_beacons add column if not exists uuid text;
alter table public.bar_beacons add column if not exists beacon_uuid uuid;
alter table public.bar_beacons add column if not exists major integer;
alter table public.bar_beacons add column if not exists minor integer;
alter table public.bar_beacons add column if not exists label text;
alter table public.bar_beacons add column if not exists min_rssi integer not null default -78;
alter table public.bar_beacons add column if not exists is_active boolean not null default true;
alter table public.bar_beacons add column if not exists status text not null default 'programmed';
alter table public.bar_beacons add column if not exists updated_at timestamptz not null default now();

update public.bar_beacons
set uuid = beacon_uuid::text
where (uuid is null or trim(uuid) = '')
  and beacon_uuid is not null;

update public.bar_beacons
set beacon_uuid = nullif(trim(uuid), '')::uuid
where beacon_uuid is null
  and uuid is not null
  and trim(uuid) ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$';

update public.bar_beacons
set is_active = coalesce(status, 'programmed') <> 'archived'
where is_active is distinct from (coalesce(status, 'programmed') <> 'archived');

create index if not exists idx_bar_beacons_checkin_lookup
on public.bar_beacons (upper(coalesce(beacon_uuid::text, uuid)), major, minor)
where is_active;

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
      and coalesce(status, 'programmed') <> 'archived'
    order by updated_at desc nulls last
    limit 1;

    if not found then
        raise exception 'beacon_inconnu';
    end if;

    if p_rssi < coalesce(v_beacon.min_rssi, -78) then
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

notify pgrst, 'reload schema';
