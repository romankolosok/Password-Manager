create extension if not exists "pg_cron" with schema "pg_catalog";

drop extension if exists "pg_net";

alter table "public"."UserProfiles" add column "EncryptedDEK" text not null default ''::text;

alter table "public"."UserProfiles" add column "RecoveryEncryptedDEK" text;

alter table "public"."UserProfiles" add column "RecoverySalt" text;

set check_function_bodies = off;

CREATE OR REPLACE FUNCTION public.prune_unconfirmed_users()
 RETURNS void
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'auth', 'public'
AS $function$
begin
  -- Delete unconfirmed users older than 7 days
  delete from auth.users u
  where u.email_confirmed_at is null
    and coalesce(u.phone_confirmed_at, u.confirmed_at) is null
    and u.created_at < now() - interval '7 days';
end;
$function$
;

CREATE OR REPLACE FUNCTION public.handle_new_user_profile()
 RETURNS trigger
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'public'
AS $function$
begin
  if new.raw_user_meta_data ? 'salt'
     and new.raw_user_meta_data ? 'encrypted_verification_token'
     and new.raw_user_meta_data ? 'encrypted_dek'
  then
    insert into public."UserProfiles"(
        "Id", "Salt", "EncryptedVerificationToken", "EncryptedDEK", "CreatedAt"
    )
    values (
      new.id,
      (new.raw_user_meta_data->>'salt'),
      (new.raw_user_meta_data->>'encrypted_verification_token'),
      (new.raw_user_meta_data->>'encrypted_dek'),
      now()
    );
  end if;
  return new;
end;
$function$
;

drop trigger if exists "on_auth_user_created_profile" on "auth"."users";

CREATE TRIGGER on_auth_user_created AFTER INSERT ON auth.users FOR EACH ROW EXECUTE FUNCTION public.handle_new_user_profile();


