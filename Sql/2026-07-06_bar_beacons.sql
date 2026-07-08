-- Spotiz / NightOut Admin - Beacons Feasycom
-- A exécuter dans Supabase SQL Editor avant d'utiliser l'association automatique.

create table if not exists public.bar_beacons (
  id uuid primary key default gen_random_uuid(),
  bar_id uuid not null references public.bars(id) on delete cascade,
  beacon_uuid uuid not null,
  major integer not null check (major between 0 and 65535),
  minor integer not null check (minor between 0 and 65535),
  device_name text not null,
  pin_code text,
  bluetooth_address text,
  broadcast_interval_ms integer not null default 1000,
  device_tx_power integer,
  beacon_tx_power integer,
  status text not null default 'programmed',
  programmed_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint bar_beacons_unique_slot unique (bar_id, major, minor)
);

-- Migration corrective si la table existait déjà avant cette version.
-- Supabase/PostgREST renvoie PGRST204 si une colonne utilisée par l'admin n'existe pas encore
-- ou si le cache de schéma n'a pas été rechargé.
alter table public.bar_beacons add column if not exists beacon_uuid uuid;
alter table public.bar_beacons add column if not exists major integer;
alter table public.bar_beacons add column if not exists minor integer;
alter table public.bar_beacons add column if not exists device_name text;
alter table public.bar_beacons add column if not exists pin_code text;
alter table public.bar_beacons add column if not exists bluetooth_address text;
alter table public.bar_beacons add column if not exists broadcast_interval_ms integer not null default 1000;
alter table public.bar_beacons add column if not exists device_tx_power integer;
alter table public.bar_beacons add column if not exists beacon_tx_power integer;
alter table public.bar_beacons add column if not exists status text not null default 'programmed';
alter table public.bar_beacons add column if not exists programmed_at timestamptz;
alter table public.bar_beacons add column if not exists created_at timestamptz not null default now();
alter table public.bar_beacons add column if not exists updated_at timestamptz not null default now();

-- Recharge le cache PostgREST utilisé par l'API Supabase.

-- Si la table existait déjà, CREATE TABLE IF NOT EXISTS ne crée PAS les contraintes manquantes.
-- Cette contrainte est indispensable pour le Upsert Supabase avec on_conflict=bar_id,major,minor.
do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname = 'bar_beacons_unique_slot'
      and conrelid = 'public.bar_beacons'::regclass
  ) then
    alter table public.bar_beacons
      add constraint bar_beacons_unique_slot unique (bar_id, major, minor);
  end if;
end $$;

-- Contrainte pratique pour éviter de rattacher deux fois la même adresse BLE active.
create unique index if not exists ux_bar_beacons_active_bluetooth_address
  on public.bar_beacons (bluetooth_address)
  where bluetooth_address is not null and bluetooth_address <> '' and status <> 'archived';

notify pgrst, 'reload schema';

create index if not exists idx_bar_beacons_bar_id on public.bar_beacons(bar_id);
create index if not exists idx_bar_beacons_identity on public.bar_beacons(beacon_uuid, major, minor);

alter table public.bar_beacons enable row level security;

-- A adapter à vos policies admin existantes si nécessaire.
-- create policy "admin full access bar_beacons" on public.bar_beacons
-- for all using (public.is_admin(auth.uid())) with check (public.is_admin(auth.uid()));
