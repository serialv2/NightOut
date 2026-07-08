-- NightOut - Nettoyage strict des doublons device_tokens.
-- Garde une seule ligne active par utilisateur : la plus recente.
-- Utile tant que l'app doit eviter absolument les push recus en double.

with ranked as (
  select
    id,
    row_number() over (
      partition by user_id
      order by coalesce(last_seen_at, created_at) desc, id desc
    ) as rn
  from public.device_tokens
  where token is not null
    and length(trim(token)) > 0
)
delete from public.device_tokens dt
using ranked r
where dt.id = r.id
  and r.rn > 1;

-- Controle apres nettoyage.
select
  user_id,
  platform,
  device_name,
  count(*) as rows_count,
  max(last_seen_at) as newest_seen_at
from public.device_tokens
group by user_id, platform, device_name
order by newest_seen_at desc;
