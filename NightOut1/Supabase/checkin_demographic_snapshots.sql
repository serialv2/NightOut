alter table public.checkins
    add column if not exists gender_snapshot text,
    add column if not exists age_snapshot int,
    add column if not exists age_band_snapshot text;

create or replace function public.compute_age_band(p_age int)
returns text
language sql
immutable
as $$
    select case
        when p_age is null then 'unknown'
        when p_age between 18 and 24 then '18_24'
        when p_age between 25 and 34 then '25_34'
        when p_age between 35 and 44 then '35_44'
        when p_age >= 45 then '45_plus'
        else 'unknown'
    end
$$;

create or replace function public.set_checkin_demographic_snapshots()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
    profile_gender text;
    profile_birthdate date;
    computed_age int;
begin
    select p.gender, p.birthdate
      into profile_gender, profile_birthdate
      from public.profiles p
     where p.id = new.user_id;

    if profile_birthdate is not null then
        computed_age := date_part('year', age(coalesce(new.checked_in_at, now()), profile_birthdate))::int;
    end if;

    new.gender_snapshot := coalesce(new.gender_snapshot, profile_gender);
    new.age_snapshot := coalesce(new.age_snapshot, computed_age);
    new.age_band_snapshot := coalesce(new.age_band_snapshot, public.compute_age_band(new.age_snapshot));

    return new;
end;
$$;

drop trigger if exists trg_set_checkin_demographic_snapshots on public.checkins;
create trigger trg_set_checkin_demographic_snapshots
before insert on public.checkins
for each row
execute function public.set_checkin_demographic_snapshots();

update public.checkins c
   set gender_snapshot = coalesce(c.gender_snapshot, p.gender),
       age_snapshot = coalesce(
           c.age_snapshot,
           case
               when p.birthdate is null then null
               else date_part('year', age(coalesce(c.checked_in_at, now()), p.birthdate))::int
           end
       )
  from public.profiles p
 where p.id = c.user_id
   and (c.gender_snapshot is null or c.age_snapshot is null or c.age_band_snapshot is null);

update public.checkins
   set age_band_snapshot = public.compute_age_band(age_snapshot)
 where age_band_snapshot is null;

create index if not exists idx_checkins_bar_gender_snapshot
    on public.checkins(bar_id, gender_snapshot);

create index if not exists idx_checkins_bar_age_band_snapshot
    on public.checkins(bar_id, age_band_snapshot);
