-- Spotiz / NightOut - Commentaires sur le fil d'activité des bars
-- À exécuter dans Supabase SQL Editor.

create table if not exists public.bar_activity_comments (
    id uuid primary key default gen_random_uuid(),
    activity_id text not null,
    activity_type text not null default 'unknown',
    user_id uuid not null references public.profiles(id) on delete cascade,
    content text not null check (char_length(trim(content)) between 1 and 500),
    created_at timestamptz not null default now()
);

create index if not exists idx_bar_activity_comments_activity
    on public.bar_activity_comments(activity_id, created_at);

create index if not exists idx_bar_activity_comments_user
    on public.bar_activity_comments(user_id, created_at desc);

alter table public.bar_activity_comments enable row level security;

drop policy if exists "bar_activity_comments_select_authenticated" on public.bar_activity_comments;
create policy "bar_activity_comments_select_authenticated"
on public.bar_activity_comments
for select
to authenticated
using (true);

drop policy if exists "bar_activity_comments_insert_own" on public.bar_activity_comments;
create policy "bar_activity_comments_insert_own"
on public.bar_activity_comments
for insert
to authenticated
with check (auth.uid() = user_id);

drop policy if exists "bar_activity_comments_delete_own" on public.bar_activity_comments;
create policy "bar_activity_comments_delete_own"
on public.bar_activity_comments
for delete
to authenticated
using (auth.uid() = user_id);

create or replace function public.get_bar_activity_comment_counts(p_activity_ids text[])
returns table(activity_id text, comment_count int)
language sql
security definer
set search_path = public
as $$
    select c.activity_id, count(*)::int as comment_count
    from public.bar_activity_comments c
    where c.activity_id = any(p_activity_ids)
    group by c.activity_id;
$$;

create or replace function public.get_bar_activity_comments(
    p_activity_id text,
    p_limit int default 50
)
returns table(
    id uuid,
    activity_id text,
    activity_type text,
    user_id uuid,
    username text,
    avatar_url text,
    content text,
    created_at timestamptz
)
language sql
security definer
set search_path = public
as $$
    select
        c.id,
        c.activity_id,
        c.activity_type,
        c.user_id,
        coalesce(nullif(p.display_name, ''), nullif(p.username, ''), 'Utilisateur') as username,
        p.avatar_url,
        c.content,
        c.created_at
    from public.bar_activity_comments c
    left join public.profiles p on p.id = c.user_id
    where c.activity_id = p_activity_id
    order by c.created_at asc
    limit greatest(1, least(coalesce(p_limit, 50), 100));
$$;

create or replace function public.post_bar_activity_comment(
    p_activity_id text,
    p_activity_type text,
    p_content text
)
returns table(
    id uuid,
    activity_id text,
    activity_type text,
    user_id uuid,
    username text,
    avatar_url text,
    content text,
    created_at timestamptz
)
language plpgsql
security definer
set search_path = public
as $$
declare
    v_comment_id uuid;
begin
    if auth.uid() is null then
        raise exception 'not_authenticated';
    end if;

    if p_activity_id is null or length(trim(p_activity_id)) = 0 then
        raise exception 'invalid_activity_id';
    end if;

    if p_content is null or length(trim(p_content)) = 0 then
        raise exception 'empty_comment';
    end if;

    if length(trim(p_content)) > 500 then
        raise exception 'comment_too_long';
    end if;

    insert into public.bar_activity_comments(activity_id, activity_type, user_id, content)
    values (trim(p_activity_id), coalesce(nullif(trim(p_activity_type), ''), 'unknown'), auth.uid(), trim(p_content))
    returning bar_activity_comments.id into v_comment_id;

    return query
    select
        c.id,
        c.activity_id,
        c.activity_type,
        c.user_id,
        coalesce(nullif(p.display_name, ''), nullif(p.username, ''), 'Utilisateur') as username,
        p.avatar_url,
        c.content,
        c.created_at
    from public.bar_activity_comments c
    left join public.profiles p on p.id = c.user_id
    where c.id = v_comment_id;
end;
$$;

grant execute on function public.get_bar_activity_comment_counts(text[]) to authenticated;
grant execute on function public.get_bar_activity_comments(text, int) to authenticated;
grant execute on function public.post_bar_activity_comment(text, text, text) to authenticated;
