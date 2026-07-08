-- NightOut - Diagnostic livraison push Supabase -> Edge Function -> Firebase.
--
-- 1) Cree une notification de test.
select public.push_notification(
  '0daa147f-cfef-4549-a115-f1187ee03b30'::uuid,
  'test_push',
  null::uuid,
  gen_random_uuid(),
  'test',
  'Diagnostic push NightOut depuis Supabase'
) as notification_id;

-- 2) Verifie que la notification existe bien en base.
select
  id,
  user_id,
  type,
  title,
  message,
  is_read,
  created_at
from public.notifications
where user_id = '0daa147f-cfef-4549-a115-f1187ee03b30'
order by created_at desc
limit 5;

-- 3) Verifie que pg_net a bien appele l'Edge Function send-push.
-- A lire :
-- status_code = 200  -> Edge Function appelee correctement.
-- content.sent        -> nombre de tokens livres par Firebase.
-- content.removed     -> vieux tokens invalides supprimes de device_tokens.
-- status_code = 401  -> Verify JWT encore actif, ou mauvais x-nightout-push-secret.
-- status_code = 404  -> Edge Function pas deployee ou mauvais nom.
-- status_code = 500  -> secret Firebase manquant/incorrect, ou erreur Firebase.
select
  id,
  status_code,
  timed_out,
  error_msg,
  content,
  created
from net._http_response
order by created desc
limit 10;

-- 4) Verifie que le token du S22 est bien celui utilise.
select
  id,
  user_id,
  platform,
  device_name,
  app_version,
  created_at,
  last_seen_at
from public.device_tokens
where token = 'cv6rKiFmQsaoi7K8Jc1yE0:APA91bEIhlgFUVDFgNVtJHlWIcm_lko_Ho7T4RpkDOOmGtkifCs_RsE96D0f-rbjfqG8imZr_kMX5IhVQXJGVGBwCOgLazSMkDr-0n9rpySVQZVzs35QgbY';
