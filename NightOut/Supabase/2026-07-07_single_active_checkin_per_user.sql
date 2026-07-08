-- Garantit qu'un utilisateur ne peut avoir qu'un seul check-in actif.
-- A executer dans Supabase SQL Editor.

begin;

-- 1) Nettoie les doublons existants : on garde le check-in actif le plus recent
--    pour chaque utilisateur, et on ferme les autres.
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

-- 2) Verrou serveur : impossible d'avoir deux lignes actives pour le meme user.
create unique index if not exists uq_checkins_one_active_per_user
    on public.checkins(user_id)
    where is_active = true;

commit;
