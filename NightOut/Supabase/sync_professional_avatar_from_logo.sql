-- NightOut - Synchronise l'avatar public des comptes pro avec leur logo.
-- A executer une fois pour rattraper les comptes existants.

with pro_logos as (
  select distinct on (pa.user_id)
    pa.user_id,
    coalesce(
      nullif(trim(pa.logo_url), ''),
      nullif(trim(b.logo_url), '')
    ) as logo_url
  from public.professional_accounts pa
  left join public.bars b
    on b.professional_account_id = pa.id
  where pa.user_id is not null
  order by
    pa.user_id,
    case when nullif(trim(pa.logo_url), '') is not null then 0 else 1 end,
    b.updated_at desc nulls last
)
update public.profiles p
set
  avatar_url = pl.logo_url,
  updated_at = now()
from pro_logos pl
where p.id = pl.user_id
  and pl.logo_url is not null
  and coalesce(p.avatar_url, '') <> pl.logo_url;

-- Controle.
select
  p.id as user_id,
  p.username,
  p.account_type,
  p.avatar_url,
  pa.display_name as professional_name,
  pa.logo_url as professional_logo_url
from public.profiles p
join public.professional_accounts pa
  on pa.user_id = p.id
order by pa.updated_at desc nulls last;
