# NightOut - Push Android FCM

Le diagnostic actuel est simple : `public.push_notification` cree les lignes dans `public.notifications`, mais n'appelle pas Firebase. Les badges et la liste interne peuvent donc marcher, mais Android ne peut pas afficher de vraie notification push.

## Fichiers ajoutes

- `functions/send-push/index.ts` : Edge Function Supabase qui envoie a Firebase Cloud Messaging.
- `push_notification_fcm_fix.sql` : remplace `public.push_notification` pour creer la notification puis appeler l'Edge Function.
- `diagnose_push_delivery.sql` : test de bout en bout et lecture des reponses `pg_net`.

## Secrets a configurer dans Supabase Edge Functions

Dans Supabase, configure ces secrets pour `send-push` :

```text
NIGHTOUT_PUSH_WEBHOOK_SECRET=une_longue_valeur_aleatoire
FIREBASE_PROJECT_ID=beauoupas-30dfc
FIREBASE_CLIENT_EMAIL=client_email_du_compte_service_firebase
FIREBASE_PRIVATE_KEY=private_key_du_compte_service_firebase
```

Le compte de service Firebase se recupere dans Firebase Console :
Project settings > Service accounts > Generate new private key.

Important : pour l'Edge Function `send-push`, desactive `Verify JWT`.
L'appel vient de la base Supabase via `pg_net`, et il est protege par le header secret `x-nightout-push-secret`.

## SQL

Dans `push_notification_fcm_fix.sql`, remplace :

```sql
v_push_secret text := 'REMPLACE_MOI_SECRET_LONG';
```

par la meme valeur que `NIGHTOUT_PUSH_WEBHOOK_SECRET`, puis execute le script dans Supabase SQL Editor.

## Test

1. Supprime les vieux tokens du meme telephone dans `device_tokens`.
2. Ouvre NightOut une fois sur le S22 pour recreer un token recent.
3. Ferme l'app ou verrouille l'ecran.
4. Envoie une notification depuis un autre compte.
5. Verifie les logs de l'Edge Function `send-push` si rien n'arrive.

La reponse de `send-push` contient :

```text
sent    = nombre de tokens livres par Firebase
failed  = nombre de tokens refuses par Firebase
removed = vieux tokens supprimes automatiquement de device_tokens
```

Si `failed` reste positif, lis le champ `failures` dans `net._http_response.content`.
