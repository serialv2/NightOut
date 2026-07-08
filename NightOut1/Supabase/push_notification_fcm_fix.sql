-- NightOut - Correctif serveur pour les vraies notifications push Android.
--
-- Diagnostic :
-- La fonction public.push_notification existante insere bien une ligne dans public.notifications,
-- mais elle n'appelle pas Firebase. Elle ne peut donc pas afficher de notification Android.
--
-- A faire avant d'executer ce script :
-- 1. Deployer l'Edge Function Supabase : Supabase/functions/send-push/index.ts
--    Important : desactiver "Verify JWT" pour cette fonction.
--    La securite passe par le header x-nightout-push-secret ci-dessous.
-- 2. Configurer les secrets de la fonction :
--    NIGHTOUT_PUSH_WEBHOOK_SECRET = une longue valeur aleatoire
--    FIREBASE_PROJECT_ID          = beauoupas-30dfc
--    FIREBASE_CLIENT_EMAIL        = client_email du compte de service Firebase
--    FIREBASE_PRIVATE_KEY         = private_key du compte de service Firebase
-- 3. Remplacer ci-dessous REMPLACE_MOI_SECRET_LONG par la meme valeur que NIGHTOUT_PUSH_WEBHOOK_SECRET.

create extension if not exists pg_net with schema extensions;

create or replace function public.push_notification(
    p_user_id uuid,
    p_type text,
    p_actor_id uuid default null::uuid,
    p_entity_id uuid default null::uuid,
    p_entity_type text default null::text,
    p_body text default null::text
)
returns uuid
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
    v_title text;
    v_message text;
    v_id uuid;
    v_actor_name text;
    v_edge_url text := 'https://keeraqtoiwvcybhavkfb.supabase.co/functions/v1/send-push';
    v_push_secret text := 'REMPLACE_MOI_SECRET_LONG';
begin
    select coalesce(display_name, username, 'Quelqu''un')
    into v_actor_name
    from public.profiles
    where id = p_actor_id;

    v_title := case p_type
        when 'friend_request' then 'Nouvelle demande d''ami'
        when 'friend_accepted' then 'Demande acceptee'
        when 'private_message' then 'Nouveau message prive'
        when 'direct_message' then 'Nouveau message prive'
        when 'group_member_added' then 'Ajoute a un groupe'
        when 'group_message' then 'Nouveau message de groupe'
        when 'group_photo' then 'Nouvelle photo de groupe'
        when 'group_video' then 'Nouvelle video de groupe'
        when 'group_event' then 'Nouvelle sortie de groupe'
        when 'group_event_response' then 'Reponse a une sortie'
        when 'ephemeral_event_friend' then 'Nouvelle sortie'
        when 'ephemeral_event_group' then 'Nouvelle sortie de groupe'
        when 'ephemeral_event_cancelled' then 'Sortie annulee'
        when 'invite_reward' then 'Credits gagnes'
        else 'Notification NightOut'
    end;

    v_message := case
        when p_body is not null and length(trim(p_body)) > 0 then
            case
                when length(trim(p_body)) > 120
                    then left(trim(p_body), 117) || '...'
                else trim(p_body)
            end
        when p_type = 'friend_request' then coalesce(v_actor_name, 'Quelqu''un') || ' souhaite vous ajouter en ami.'
        when p_type = 'friend_accepted' then coalesce(v_actor_name, 'Quelqu''un') || ' a accepte votre demande d''ami.'
        when p_type in ('private_message', 'direct_message') then coalesce(v_actor_name, 'Quelqu''un') || ' vous a envoye un message.'
        when p_type = 'group_member_added' then coalesce(v_actor_name, 'Quelqu''un') || ' vous a ajoute a un groupe.'
        when p_type = 'group_message' then coalesce(v_actor_name, 'Quelqu''un') || ' a envoye un message dans un groupe.'
        when p_type = 'group_photo' then coalesce(v_actor_name, 'Quelqu''un') || ' a envoye une photo dans un groupe.'
        when p_type = 'group_video' then coalesce(v_actor_name, 'Quelqu''un') || ' a envoye une video dans un groupe.'
        when p_type = 'group_event' then 'Une sortie vient d''etre proposee.'
        when p_type = 'group_event_response' then coalesce(v_actor_name, 'Quelqu''un') || ' a repondu a une sortie.'
        when p_type = 'ephemeral_event_cancelled' then 'Une sortie a ete annulee.'
        when p_type = 'invite_reward' then 'Vous avez gagne des credits.'
        else 'Nouvelle notification NightOut.'
    end;

    insert into public.notifications (
        user_id,
        type,
        title,
        message,
        actor_id,
        entity_id,
        entity_type,
        is_read,
        created_at
    )
    values (
        p_user_id,
        p_type,
        v_title,
        v_message,
        p_actor_id,
        p_entity_id,
        p_entity_type,
        false,
        now()
    )
    returning id into v_id;

    -- Appel asynchrone : la notification reste creee meme si Firebase est temporairement indisponible.
    perform net.http_post(
        url := v_edge_url,
        headers := jsonb_build_object(
            'Content-Type', 'application/json',
            'x-nightout-push-secret', v_push_secret
        ),
        body := jsonb_build_object('notification_id', v_id)
    );

    return v_id;
end;
$$;

-- Evite que l'ancien overload a 5 parametres continue a etre appele par erreur.
create or replace function public.push_notification(
    p_user_id uuid,
    p_type text,
    p_actor_id uuid default null::uuid,
    p_entity_id uuid default null::uuid,
    p_entity_type text default null::text
)
returns uuid
language sql
security definer
set search_path = public
as $$
    select public.push_notification($1, $2, $3, $4, $5, null::text);
$$;
