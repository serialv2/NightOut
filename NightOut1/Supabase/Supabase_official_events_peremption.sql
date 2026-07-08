-- NightOut - Péremption des événements officiels
-- Règle : si end_at est renseigné, il fait foi.
-- Sinon, l'événement expire automatiquement 8 heures après start_at.

-- 1) Vue pratique pour contrôler les dates effectives.
create or replace view public.official_events_with_effective_end as
select
    oe.*,
    coalesce(oe.end_at, oe.end_date, oe.start_at + interval '8 hours', oe.start_date + interval '8 hours') as effective_end_at,
    case
        when oe.status = 'published'
         and oe.is_active = true
         and coalesce(oe.end_at, oe.end_date, oe.start_at + interval '8 hours', oe.start_date + interval '8 hours') < now()
        then true
        else false
    end as should_expire
from public.official_events oe;

-- 2) Fonction de nettoyage manuel ou automatisable plus tard avec Supabase Cron.
create or replace function public.expire_old_official_events()
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
    affected_count integer;
begin
    update public.official_events
    set
        status = 'expired',
        is_active = false,
        updated_at = now()
    where status = 'published'
      and is_active = true
      and coalesce(end_at, end_date, start_at + interval '8 hours', start_date + interval '8 hours') < now();

    get diagnostics affected_count = row_count;
    return affected_count;
end;
$$;

-- 3) Exécution immédiate du nettoyage.
select public.expire_old_official_events() as events_expired;

-- 4) Requête de contrôle.
select
    id,
    title,
    start_at,
    end_at,
    effective_end_at,
    status,
    is_active,
    should_expire
from public.official_events_with_effective_end
order by start_at desc;
