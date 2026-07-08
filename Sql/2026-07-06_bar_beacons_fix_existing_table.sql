-- Spotiz / NightOut Admin - Correctif table bar_beacons existante
-- A exécuter dans Supabase SQL Editor si tu as l'erreur :
-- PGRST204 Could not find the 'beacon_tx_power' column of 'bar_beacons' in the schema cache

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

create index if not exists idx_bar_beacons_bar_id on public.bar_beacons(bar_id);
create index if not exists idx_bar_beacons_identity on public.bar_beacons(beacon_uuid, major, minor);


-- Nécessaire pour l'UPSERT PostgREST : on_conflict=bar_id,major,minor
-- Si des doublons existent déjà, on conserve la ligne la plus récente.
delete from public.bar_beacons a
using public.bar_beacons b
where a.ctid < b.ctid
  and coalesce(a.bar_id::text, '') = coalesce(b.bar_id::text, '')
  and coalesce(a.major, -1) = coalesce(b.major, -1)
  and coalesce(a.minor, -1) = coalesce(b.minor, -1);

create unique index if not exists bar_beacons_unique_bar_major_minor
    on public.bar_beacons(bar_id, major, minor);

notify pgrst, 'reload schema';
