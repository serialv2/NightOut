-- Corrige la RPC de check-in manuel pour permettre un nouveau check-in
-- apres un check-out volontaire.
-- A executer dans Supabase SQL Editor.

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
    p_event_id uuid default null
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
            p_event_id,
            now(),
            true
        )
        returning * into v_checkin;
    end if;

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
