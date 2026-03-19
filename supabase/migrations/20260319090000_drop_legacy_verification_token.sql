-- Drop legacy verification-token design and keep only DEK/KEK accounts.

-- 1) Update profile creation trigger to insert only when encrypted_dek exists.
CREATE OR REPLACE FUNCTION public.handle_new_user_profile()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'public'
AS $$
begin
  if new.raw_user_meta_data ? 'salt'
     and new.raw_user_meta_data ? 'encrypted_dek'
  then
    insert into public."UserProfiles"(
      "Id",
      "Salt",
      "EncryptedDEK",
      "CreatedAt"
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
$$;

-- Replace triggers to avoid multiple competing ones.
DROP TRIGGER IF EXISTS "on_auth_user_created_profile" ON "auth"."users";
DROP TRIGGER IF EXISTS "on_auth_user_created" ON "auth"."users";

CREATE TRIGGER "on_auth_user_created"
AFTER INSERT ON "auth"."users"
FOR EACH ROW
EXECUTE FUNCTION public.handle_new_user_profile();

-- 2) Safety wipe (optional; column is about to be dropped).
UPDATE public."UserProfiles"
SET "EncryptedVerificationToken" = ''
WHERE "EncryptedVerificationToken" IS NOT NULL;

-- 3) Drop the legacy column.
ALTER TABLE public."UserProfiles"
DROP COLUMN IF EXISTS "EncryptedVerificationToken";

