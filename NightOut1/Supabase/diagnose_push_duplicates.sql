-- NightOut - Diagnostic push recus en double.

-- 1) Verifie combien de tokens actifs existent encore pour le compte.
select
  user_id,
  id,
  platform,
  device_name,
  left(token, 18) || '...' as token_preview,
  created_at,
  last_seen_at
from public.device_tokens
where user_id = '0daa147f-cfef-4549-a115-f1187ee03b30'
order by coalesce(last_seen_at, created_at) desc;

-- 2) Verifie combien d'envois Firebase l'Edge Function declare.
select
  id,
  status_code,
  content,
  created
from net._http_response
order by created desc
limit 10;

-- Si content contient "sent":2, le serveur envoyait deux push.
-- Si content contient "sent":1 mais le telephone affiche deux notifications,
-- il faut installer la derniere version mobile anti-notification-locale-double.
