-- Diagnostic lecture seule du système de récompenses Spotiz.
-- À lancer dans Supabase SQL Editor si l'app affiche encore une erreur serveur.

select
    n.nspname as schema_name,
    p.proname as function_name,
    pg_get_function_identity_arguments(p.oid) as arguments
from pg_proc p
join pg_namespace n on n.oid = p.pronamespace
where n.nspname = 'public'
  and p.proname in ('create_reward_redemption_intent', 'redeem_reward_token', 'get_bar_reward_redemptions')
order by p.proname, arguments;

select
    table_name
from information_schema.tables
where table_schema = 'public'
  and table_name in ('bar_rewards', 'reward_redemption_intents', 'reward_redemptions', 'user_credits', 'credit_transactions')
order by table_name;

select
    id,
    bar_id,
    title,
    points_cost,
    is_active,
    starts_at,
    ends_at
from public.bar_rewards
order by created_at desc
limit 10;
