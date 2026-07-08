-- Diagnostic beacon Spotiz.
-- Remplace les 3 valeurs ci-dessous par celles affichees dans le toast mobile
-- "Beacon detecte: UUID / Major / Minor (... dBm)".

with detected as (
  select
    'FDA50693-A4E2-4FB1-AFCF-C6EB07647825'::text as uuid,
    0::integer as major,
    0::integer as minor
)
select
  b.id,
  b.bar_id,
  bars.name as bar_name,
  coalesce(b.beacon_uuid::text, b.uuid) as saved_uuid,
  b.major,
  b.minor,
  b.min_rssi,
  b.is_active,
  b.status,
  b.bluetooth_address,
  b.updated_at,
  case
    when upper(coalesce(b.beacon_uuid::text, b.uuid)) = upper(d.uuid)
     and b.major = d.major
     and b.minor = d.minor
     and b.is_active = true
     and coalesce(b.status, 'programmed') <> 'archived'
    then 'OK_MATCH'
    else 'NO_MATCH'
  end as match_status
from public.bar_beacons b
left join public.bars on bars.id = b.bar_id
cross join detected d
where upper(coalesce(b.beacon_uuid::text, b.uuid)) = upper(d.uuid)
   or b.bluetooth_address is not null
order by
  case
    when upper(coalesce(b.beacon_uuid::text, b.uuid)) = upper(d.uuid)
     and b.major = d.major
     and b.minor = d.minor
    then 0 else 1
  end,
  b.updated_at desc nulls last;
