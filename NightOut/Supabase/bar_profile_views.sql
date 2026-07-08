create table if not exists public.bar_profile_views (
    id uuid primary key default gen_random_uuid(),
    bar_id uuid not null references public.bars(id) on delete cascade,
    viewer_id uuid not null references public.profiles(id) on delete cascade,
    viewed_at timestamptz not null default now()
);

alter table public.bar_profile_views enable row level security;

create index if not exists idx_bar_profile_views_bar_id
    on public.bar_profile_views(bar_id);

create index if not exists idx_bar_profile_views_viewer_id
    on public.bar_profile_views(viewer_id);

create index if not exists idx_bar_profile_views_viewed_at
    on public.bar_profile_views(viewed_at desc);

drop policy if exists "Users can create own bar profile views" on public.bar_profile_views;
create policy "Users can create own bar profile views"
on public.bar_profile_views
for insert
to authenticated
with check (auth.uid() = viewer_id);

drop policy if exists "Admins can read all bar profile views" on public.bar_profile_views;
create policy "Admins can read all bar profile views"
on public.bar_profile_views
for select
to authenticated
using (
    exists (
        select 1
        from public.profiles p
        where p.id = auth.uid()
          and coalesce(p.is_admin, false) = true
    )
);

create or replace function public.get_bar_profile_view_stats(p_bar_id uuid default null)
returns table (
    view_total int,
    view_female int,
    view_male int,
    view_unknown int
)
language plpgsql
security definer
set search_path = public
as $$
declare
    current_user_id uuid := auth.uid();
    current_user_is_admin boolean := false;
begin
    select coalesce(p.is_admin, false)
      into current_user_is_admin
      from public.profiles p
     where p.id = current_user_id;

    return query
    with visible_bars as (
        select b.id
          from public.bars b
          left join public.professional_accounts pa
            on pa.id = b.professional_account_id
         where (p_bar_id is null or b.id = p_bar_id)
           and (
                current_user_is_admin
                or b.owner_id = current_user_id
                or pa.user_id = current_user_id
           )
    )
    select
        count(v.id)::int as view_total,
        count(*) filter (
            where lower(coalesce(p.gender, '')) in ('femme', 'female', 'woman')
        )::int as view_female,
        count(*) filter (
            where lower(coalesce(p.gender, '')) in ('homme', 'male', 'man')
        )::int as view_male,
        count(*) filter (
            where lower(coalesce(p.gender, '')) not in ('femme', 'female', 'woman', 'homme', 'male', 'man')
        )::int as view_unknown
    from public.bar_profile_views v
    inner join visible_bars b on b.id = v.bar_id
    left join public.profiles p on p.id = v.viewer_id;
end;
$$;

grant execute on function public.get_bar_profile_view_stats(uuid) to authenticated;
