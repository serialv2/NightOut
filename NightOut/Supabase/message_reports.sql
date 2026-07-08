create table if not exists public.message_reports (
  id uuid primary key default gen_random_uuid(),
  reporter_id uuid not null references auth.users(id) on delete cascade,
  reported_user_id uuid not null references auth.users(id) on delete cascade,
  direct_message_id uuid not null references public.direct_messages(id) on delete cascade,
  conversation_partner_id uuid not null references auth.users(id) on delete cascade,
  reason text not null default 'other',
  message_content_snapshot text,
  status text not null default 'pending',
  admin_note text,
  reviewed_by uuid references auth.users(id),
  reviewed_at timestamptz,
  created_at timestamptz not null default now(),
  constraint message_reports_reason_check
    check (reason in ('harassment', 'spam', 'inappropriate', 'threat', 'other')),
  constraint message_reports_status_check
    check (status in ('pending', 'reviewed', 'dismissed', 'action_taken'))
);

alter table public.message_reports enable row level security;

drop policy if exists "Users can create message reports" on public.message_reports;

create policy "Users can create message reports"
on public.message_reports
for insert
to authenticated
with check (reporter_id = auth.uid());

drop policy if exists "Users can read their own message reports" on public.message_reports;

create policy "Users can read their own message reports"
on public.message_reports
for select
to authenticated
using (reporter_id = auth.uid());

drop policy if exists "Admins can manage message reports" on public.message_reports;

create policy "Admins can manage message reports"
on public.message_reports
for all
to authenticated
using (public.is_admin())
with check (public.is_admin());

create or replace function public.report_direct_message(
  p_direct_message_id uuid,
  p_reported_user_id uuid,
  p_conversation_partner_id uuid,
  p_reason text default 'other',
  p_message_content_snapshot text default null
)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
  v_message public.direct_messages%rowtype;
  v_report_id uuid;
begin
  select *
  into v_message
  from public.direct_messages
  where id = p_direct_message_id
    and (sender_id = auth.uid() or receiver_id = auth.uid());

  if not found then
    raise exception 'Message introuvable ou non autorisé';
  end if;

  if p_reported_user_id not in (v_message.sender_id, v_message.receiver_id)
     or p_reported_user_id = auth.uid() then
    raise exception 'Utilisateur signalé invalide';
  end if;

  insert into public.message_reports (
    reporter_id,
    reported_user_id,
    direct_message_id,
    conversation_partner_id,
    reason,
    message_content_snapshot
  )
  values (
    auth.uid(),
    p_reported_user_id,
    p_direct_message_id,
    p_conversation_partner_id,
    coalesce(nullif(p_reason, ''), 'other'),
    p_message_content_snapshot
  )
  returning id into v_report_id;

  return v_report_id;
end;
$$;

grant execute on function public.report_direct_message(uuid, uuid, uuid, text, text) to authenticated;
