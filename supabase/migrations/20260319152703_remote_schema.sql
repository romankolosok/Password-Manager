set check_function_bodies = off;

CREATE OR REPLACE FUNCTION public.handle_new_user_profile()
 RETURNS trigger
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'public'
AS $function$
begin
  if new.raw_user_meta_data ? 'salt'
     and new.raw_user_meta_data ? 'encrypted_dek'
  then
    insert into public."UserProfiles"(
        "Id", "Salt", "EncryptedDEK", "CreatedAt"
    )
    values (
      new.id,
      (new.raw_user_meta_data->>'salt'),
      (new.raw_user_meta_data->>'encrypted_dek'),
      now()
    );
  end if;

  return new;
end;
$function$
;

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
    -- If you also care about phone/other confirmations, keep them if any confirmed:
    and coalesce(u.phone_confirmed_at, u.confirmed_at) is null
    and u.created_at < now() - interval '7 days';
end;
$function$
;


