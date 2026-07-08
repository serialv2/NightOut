alter table public.profiles
  add column if not exists is_banned boolean not null default false,
  add column if not exists ban_reason text,
  add column if not exists banned_at timestamptz,
  add column if not exists banned_by uuid references auth.users(id),
  add column if not exists moderation_warning_count integer not null default 0;

create table if not exists public.moderation_warnings (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  report_id uuid references public.message_reports(id) on delete set null,
  direct_message_id uuid references public.direct_messages(id) on delete set null,
  reason text not null default 'other',
  message text not null,
  created_by uuid references auth.users(id),
  created_at timestamptz not null default now()
);

alter table public.moderation_warnings enable row level security;

drop policy if exists "Admins can manage moderation warnings" on public.moderation_warnings;

create policy "Admins can manage moderation warnings"
on public.moderation_warnings
for all
to authenticated
using (public.is_admin())
with check (public.is_admin());

drop policy if exists "Users can read their own moderation warnings" on public.moderation_warnings;

create policy "Users can read their own moderation warnings"
on public.moderation_warnings
for select
to authenticated
using (user_id = auth.uid());

drop policy if exists "Admins can insert moderation notifications" on public.notifications;

create policy "Admins can insert moderation notifications"
on public.notifications
for insert
to authenticated
with check (public.is_admin());

drop policy if exists "Admins can send moderation direct messages" on public.direct_messages;

create policy "Admins can send moderation direct messages"
on public.direct_messages
for insert
to authenticated
with check (public.is_admin() and sender_id = auth.uid());

drop policy if exists "Admins can update moderation profile fields" on public.profiles;

create policy "Admins can update moderation profile fields"
on public.profiles
for update
to authenticated
using (public.is_admin())
with check (public.is_admin());
