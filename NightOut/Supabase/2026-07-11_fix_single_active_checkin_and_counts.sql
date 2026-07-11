-- Corrige les presences en double et verrouille la base :
-- un utilisateur ne peut avoir qu'un seul check-in actif a la fois.
-- A executer dans Supabase SQL Editor.

begin;

-- 1) On garde uniquement le check-in actif le plus recent pour chaque utilisateur.
with ranked as (
    select
        id,
        row_number() over (
            partition by user_id
            order by checked_in_at desc nulls last, id desc
        ) as rn
    from public.checkins
    where is_active = true
)
update public.checkins c
set
    is_active = false,
    checked_out_at = coalesce(c.checked_out_at, now())
from ranked r
where c.id = r.id
  and r.rn > 1;

-- 2) Verrou serveur : impossible de recreer deux check-ins actifs pour le meme utilisateur.
create unique index if not exists uq_checkins_one_active_per_user
    on public.checkins(user_id)
    where is_active = true;

-- 3) Recalcule les compteurs bars.total_present depuis la verite : les check-ins actifs.
with active_counts as (
    select
        bar_id,
        count(*)::integer as total
    from public.checkins
    where is_active = true
    group by bar_id
)
update public.bars b
set total_present = coalesce(ac.total, 0)
from active_counts ac
where b.id = ac.bar_id;

update public.bars b
set total_present = 0
where not exists (
    select 1
    from public.checkins c
    where c.bar_id = b.id
      and c.is_active = true
);

commit;
