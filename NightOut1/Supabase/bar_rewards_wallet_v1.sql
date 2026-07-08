-- Spotiz rewards / wallet-ready ledger v1
-- Run this script in Supabase SQL editor.

create extension if not exists pgcrypto;

create table if not exists public.bar_rewards (
    id uuid primary key default gen_random_uuid(),
    bar_id uuid not null references public.bars(id) on delete cascade,
    title text not null,
    description text,
    points_cost integer not null check (points_cost > 0),
    is_active boolean not null default true,
    max_per_user_per_day integer check (max_per_user_per_day is null or max_per_user_per_day > 0),
    starts_at timestamptz,
    ends_at timestamptz,
    created_by uuid references auth.users(id),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists idx_bar_rewards_bar_active
    on public.bar_rewards(bar_id, is_active, points_cost);

create table if not exists public.reward_redemption_intents (
    id uuid primary key default gen_random_uuid(),
    token text not null unique,
    short_code text not null,
    user_id uuid not null references auth.users(id) on delete cascade,
    bar_id uuid not null references public.bars(id) on delete cascade,
    reward_id uuid not null references public.bar_rewards(id) on delete cascade,
    points_cost_snapshot integer not null check (points_cost_snapshot > 0),
    status text not null default 'pending' check (status in ('pending', 'redeemed', 'expired', 'cancelled')),
    expires_at timestamptz not null,
    created_at timestamptz not null default now(),
    redeemed_at timestamptz,
    redeemed_by uuid references auth.users(id)
);

create index if not exists idx_reward_intents_token
    on public.reward_redemption_intents(token);

create index if not exists idx_reward_intents_short_bar_status
    on public.reward_redemption_intents(short_code, bar_id, status, expires_at);

create index if not exists idx_reward_intents_user_reward_created
    on public.reward_redemption_intents(user_id, reward_id, created_at);

create table if not exists public.reward_redemptions (
    id uuid primary key default gen_random_uuid(),
    intent_id uuid not null unique references public.reward_redemption_intents(id) on delete restrict,
    user_id uuid not null references auth.users(id) on delete cascade,
    bar_id uuid not null references public.bars(id) on delete cascade,
    reward_id uuid not null references public.bar_rewards(id) on delete restrict,
    points_cost integer not null check (points_cost > 0),
    validated_by uuid references auth.users(id),
    created_at timestamptz not null default now()
);

create index if not exists idx_reward_redemptions_bar_created
    on public.reward_redemptions(bar_id, created_at desc);

create index if not exists idx_reward_redemptions_user_created
    on public.reward_redemptions(user_id, created_at desc);

alter table public.bar_rewards enable row level security;
alter table public.reward_redemption_intents enable row level security;
alter table public.reward_redemptions enable row level security;

drop policy if exists "Rewards are public readable" on public.bar_rewards;
create policy "Rewards are public readable"
on public.bar_rewards for select
using (true);

drop policy if exists "Bar owners can manage rewards" on public.bar_rewards;
create policy "Bar owners can manage rewards"
on public.bar_rewards for all
using (
    exists (
        select 1
        from public.bars b
        left join public.professional_accounts pa on pa.id = b.professional_account_id
        where b.id = bar_rewards.bar_id
          and (b.owner_id = auth.uid() or pa.user_id = auth.uid())
    )
)
with check (
    exists (
        select 1
        from public.bars b
        left join public.professional_accounts pa on pa.id = b.professional_account_id
        where b.id = bar_rewards.bar_id
          and (b.owner_id = auth.uid() or pa.user_id = auth.uid())
    )
);

drop policy if exists "Users can read own reward intents" on public.reward_redemption_intents;
create policy "Users can read own reward intents"
on public.reward_redemption_intents for select
using (user_id = auth.uid() or redeemed_by = auth.uid());

drop policy if exists "Users can read own reward redemptions" on public.reward_redemptions;
create policy "Users can read own reward redemptions"
on public.reward_redemptions for select
using (user_id = auth.uid() or validated_by = auth.uid());

drop function if exists public.create_reward_redemption_intent(uuid);
drop function if exists public.redeem_reward_token(text, text, uuid);

create or replace function public.create_reward_redemption_intent(p_reward_id text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_reward_id uuid;
    v_reward public.bar_rewards%rowtype;
    v_balance integer := 0;
    v_token text;
    v_short_code text;
    v_intent_id uuid;
    v_expires_at timestamptz := now() + interval '2 minutes';
    v_used_today integer;
begin
    if v_user_id is null then
        return jsonb_build_object('ok', false, 'error', 'not_authenticated');
    end if;

    begin
        v_reward_id := nullif(trim(coalesce(p_reward_id, '')), '')::uuid;
    exception
        when invalid_text_representation then
            return jsonb_build_object('ok', false, 'error', 'invalid_reward_id');
    end;

    if v_reward_id is null then
        return jsonb_build_object('ok', false, 'error', 'invalid_reward_id');
    end if;

    select *
    into v_reward
    from public.bar_rewards
    where id = v_reward_id
      and is_active = true
      and (starts_at is null or starts_at <= now())
      and (ends_at is null or ends_at >= now())
    limit 1;

    if not found then
        return jsonb_build_object('ok', false, 'error', 'reward_not_available');
    end if;

    if v_reward.max_per_user_per_day is not null then
        select count(*)
        into v_used_today
        from public.reward_redemptions
        where user_id = v_user_id
          and reward_id = v_reward.id
          and created_at >= date_trunc('day', now());

        if v_used_today >= v_reward.max_per_user_per_day then
            return jsonb_build_object('ok', false, 'error', 'daily_limit_reached');
        end if;
    end if;

    select coalesce(balance, 0)
    into v_balance
    from public.user_credits
    where user_id = v_user_id;

    if coalesce(v_balance, 0) < v_reward.points_cost then
        return jsonb_build_object(
            'ok', false,
            'error', 'insufficient_balance',
            'balance', coalesce(v_balance, 0),
            'points_cost', v_reward.points_cost,
            'title', v_reward.title
        );
    end if;

    update public.reward_redemption_intents
    set status = 'expired'
    where user_id = v_user_id
      and reward_id = v_reward.id
      and status = 'pending'
      and expires_at <= now();

    v_token := replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', '');
    v_short_code := upper(substr(replace(gen_random_uuid()::text, '-', ''), 1, 6));

    insert into public.reward_redemption_intents (
        token,
        short_code,
        user_id,
        bar_id,
        reward_id,
        points_cost_snapshot,
        expires_at
    )
    values (
        v_token,
        v_short_code,
        v_user_id,
        v_reward.bar_id,
        v_reward.id,
        v_reward.points_cost,
        v_expires_at
    )
    returning id into v_intent_id;

    return jsonb_build_object(
        'ok', true,
        'intent_id', v_intent_id,
        'token', v_token,
        'short_code', v_short_code,
        'expires_at', v_expires_at,
        'title', v_reward.title,
        'points_cost', v_reward.points_cost,
        'balance', coalesce(v_balance, 0)
    );
end;
$$;

create or replace function public.redeem_reward_token(
    p_token text default null,
    p_short_code text default null,
    p_bar_id text default null
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_validator_id uuid := auth.uid();
    v_bar_id uuid;
    v_intent public.reward_redemption_intents%rowtype;
    v_reward public.bar_rewards%rowtype;
    v_balance integer := 0;
    v_redemption_id uuid;
    v_is_authorized boolean := false;
begin
    if v_validator_id is null then
        return jsonb_build_object('ok', false, 'error', 'not_authenticated');
    end if;

    if nullif(trim(coalesce(p_token, '')), '') is null
       and nullif(trim(coalesce(p_short_code, '')), '') is null then
        return jsonb_build_object('ok', false, 'error', 'invalid_token');
    end if;

    if nullif(trim(coalesce(p_bar_id, '')), '') is not null then
        begin
            v_bar_id := trim(p_bar_id)::uuid;
        exception
            when invalid_text_representation then
                return jsonb_build_object('ok', false, 'error', 'invalid_token');
        end;
    end if;

    select *
    into v_intent
    from public.reward_redemption_intents
    where status = 'pending'
      and expires_at > now()
      and (
          (p_token is not null and token = trim(p_token))
          or (
              p_short_code is not null
              and short_code = upper(trim(p_short_code))
              and (v_bar_id is null or bar_id = v_bar_id)
          )
      )
    order by created_at desc
    limit 1
    for update;

    if not found then
        return jsonb_build_object('ok', false, 'error', 'invalid_token');
    end if;

    select *
    into v_reward
    from public.bar_rewards
    where id = v_intent.reward_id
    for update;

    if not found or v_reward.is_active = false then
        return jsonb_build_object('ok', false, 'error', 'reward_not_available');
    end if;

    select exists (
        select 1
        from public.bars b
        left join public.professional_accounts pa on pa.id = b.professional_account_id
        where b.id = v_intent.bar_id
          and (b.owner_id = v_validator_id or pa.user_id = v_validator_id)
    )
    into v_is_authorized;

    if not v_is_authorized then
        return jsonb_build_object('ok', false, 'error', 'not_authorized');
    end if;

    select coalesce(balance, 0)
    into v_balance
    from public.user_credits
    where user_id = v_intent.user_id
    for update;

    if coalesce(v_balance, 0) < v_intent.points_cost_snapshot then
        return jsonb_build_object(
            'ok', false,
            'error', 'insufficient_balance',
            'balance', coalesce(v_balance, 0),
            'points_cost', v_intent.points_cost_snapshot,
            'title', v_reward.title
        );
    end if;

    update public.user_credits
    set balance = balance - v_intent.points_cost_snapshot,
        updated_at = now()
    where user_id = v_intent.user_id;

    insert into public.credit_transactions (
        id,
        user_id,
        amount,
        reason,
        entity_id,
        entity_type,
        rule_key,
        created_at
    )
    values (
        gen_random_uuid(),
        v_intent.user_id,
        -v_intent.points_cost_snapshot,
        'bar_reward_redemption',
        v_reward.id,
        'bar_reward',
        'bar_reward_redemption',
        now()
    );

    insert into public.reward_redemptions (
        intent_id,
        user_id,
        bar_id,
        reward_id,
        points_cost,
        validated_by
    )
    values (
        v_intent.id,
        v_intent.user_id,
        v_intent.bar_id,
        v_intent.reward_id,
        v_intent.points_cost_snapshot,
        v_validator_id
    )
    returning id into v_redemption_id;

    update public.reward_redemption_intents
    set status = 'redeemed',
        redeemed_at = now(),
        redeemed_by = v_validator_id
    where id = v_intent.id;

    return jsonb_build_object(
        'ok', true,
        'redemption_id', v_redemption_id,
        'title', v_reward.title,
        'points_cost', v_intent.points_cost_snapshot,
        'balance', v_balance - v_intent.points_cost_snapshot
    );
exception
    when unique_violation then
        return jsonb_build_object('ok', false, 'error', 'already_redeemed');
end;
$$;

create or replace function public.get_bar_reward_redemptions(
    p_bar_id text,
    p_limit integer default 30
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_bar_id uuid;
    v_is_authorized boolean := false;
begin
    if v_user_id is null then
        return '[]'::jsonb;
    end if;

    begin
        v_bar_id := nullif(trim(coalesce(p_bar_id, '')), '')::uuid;
    exception
        when invalid_text_representation then
            return '[]'::jsonb;
    end;

    if v_bar_id is null then
        return '[]'::jsonb;
    end if;

    select exists (
        select 1
        from public.bars b
        left join public.professional_accounts pa on pa.id = b.professional_account_id
        where b.id = v_bar_id
          and (b.owner_id = v_user_id or pa.user_id = v_user_id)
    )
    into v_is_authorized;

    if not v_is_authorized then
        return '[]'::jsonb;
    end if;

    return coalesce((
        select jsonb_agg(
            jsonb_build_object(
                'redemption_id', rr.id,
                'reward_id', rr.reward_id,
                'reward_title', coalesce(br.title, 'Récompense'),
                'points_cost', rr.points_cost,
                'user_id', rr.user_id,
                'user_display_name', coalesce(nullif(p.display_name, ''), nullif(p.username, ''), 'Client Spotiz'),
                'validated_by', rr.validated_by,
                'created_at', rr.created_at
            )
            order by rr.created_at desc
        )
        from (
            select *
            from public.reward_redemptions
            where bar_id = v_bar_id
            order by created_at desc
            limit greatest(1, least(coalesce(p_limit, 30), 100))
        ) rr
        left join public.bar_rewards br on br.id = rr.reward_id
        left join public.profiles p on p.id = rr.user_id
    ), '[]'::jsonb);
end;
$$;

grant execute on function public.create_reward_redemption_intent(text) to authenticated;
grant execute on function public.redeem_reward_token(text, text, text) to authenticated;
grant execute on function public.get_bar_reward_redemptions(text, integer) to authenticated;

notify pgrst, 'reload schema';
