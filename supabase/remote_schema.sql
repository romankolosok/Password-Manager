


SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;


COMMENT ON SCHEMA "public" IS 'standard public schema';



CREATE EXTENSION IF NOT EXISTS "pg_graphql" WITH SCHEMA "graphql";






CREATE EXTENSION IF NOT EXISTS "pg_stat_statements" WITH SCHEMA "extensions";






CREATE EXTENSION IF NOT EXISTS "pgcrypto" WITH SCHEMA "extensions";






CREATE EXTENSION IF NOT EXISTS "supabase_vault" WITH SCHEMA "vault";






CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA "extensions";






CREATE OR REPLACE FUNCTION "public"."handle_new_user_profile"() RETURNS "trigger"
    LANGUAGE "plpgsql" SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
begin
  if new.raw_user_meta_data ? 'salt' and new.raw_user_meta_data ? 'encrypted_verification_token' then
    insert into public."UserProfiles"("Id", "Salt", "EncryptedVerificationToken", "CreatedAt")
    values (
      new.id,
      (new.raw_user_meta_data->>'salt'),
      (new.raw_user_meta_data->>'encrypted_verification_token'),
      now()
    );
  end if;
  return new;
end;
$$;


ALTER FUNCTION "public"."handle_new_user_profile"() OWNER TO "postgres";

SET default_tablespace = '';

SET default_table_access_method = "heap";


CREATE TABLE IF NOT EXISTS "public"."UserProfiles" (
    "Id" "uuid" NOT NULL,
    "Salt" "text" NOT NULL,
    "EncryptedVerificationToken" "text" NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT "now"() NOT NULL
);


ALTER TABLE "public"."UserProfiles" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."VaultEntries" (
    "Id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "UserId" "uuid" NOT NULL,
    "EncryptedData" "text" NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT "now"() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT "now"() NOT NULL
);


ALTER TABLE "public"."VaultEntries" OWNER TO "postgres";


ALTER TABLE ONLY "public"."UserProfiles"
    ADD CONSTRAINT "UserProfiles_pkey" PRIMARY KEY ("Id");



ALTER TABLE ONLY "public"."VaultEntries"
    ADD CONSTRAINT "VaultEntries_pkey" PRIMARY KEY ("Id");



CREATE INDEX "idx_vault_entries_user_id" ON "public"."VaultEntries" USING "btree" ("UserId");



ALTER TABLE ONLY "public"."UserProfiles"
    ADD CONSTRAINT "UserProfiles_Id_fkey" FOREIGN KEY ("Id") REFERENCES "auth"."users"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."VaultEntries"
    ADD CONSTRAINT "VaultEntries_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "auth"."users"("id") ON DELETE CASCADE;



ALTER TABLE "public"."UserProfiles" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "Users can delete own vault entries" ON "public"."VaultEntries" FOR DELETE TO "authenticated" USING (("auth"."uid"() = "UserId"));



CREATE POLICY "Users can insert own profile" ON "public"."UserProfiles" FOR INSERT TO "authenticated" WITH CHECK (("auth"."uid"() = "Id"));



CREATE POLICY "Users can insert own vault entries" ON "public"."VaultEntries" FOR INSERT TO "authenticated" WITH CHECK (("auth"."uid"() = "UserId"));



CREATE POLICY "Users can read own profile" ON "public"."UserProfiles" FOR SELECT TO "authenticated" USING (("auth"."uid"() = "Id"));



CREATE POLICY "Users can read own vault entries" ON "public"."VaultEntries" FOR SELECT TO "authenticated" USING (("auth"."uid"() = "UserId"));



CREATE POLICY "Users can update own profile" ON "public"."UserProfiles" FOR UPDATE TO "authenticated" USING (("auth"."uid"() = "Id")) WITH CHECK (("auth"."uid"() = "Id"));



CREATE POLICY "Users can update own vault entries" ON "public"."VaultEntries" FOR UPDATE TO "authenticated" USING (("auth"."uid"() = "UserId"));



ALTER TABLE "public"."VaultEntries" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "profiles_insert_own" ON "public"."UserProfiles" FOR INSERT TO "authenticated" WITH CHECK (("auth"."uid"() = "Id"));



CREATE POLICY "profiles_select_own" ON "public"."UserProfiles" FOR SELECT TO "authenticated" USING (("auth"."uid"() = "Id"));



CREATE POLICY "profiles_update_own" ON "public"."UserProfiles" FOR UPDATE TO "authenticated" USING (("auth"."uid"() = "Id"));





ALTER PUBLICATION "supabase_realtime" OWNER TO "postgres";


GRANT USAGE ON SCHEMA "public" TO "postgres";
GRANT USAGE ON SCHEMA "public" TO "anon";
GRANT USAGE ON SCHEMA "public" TO "authenticated";
GRANT USAGE ON SCHEMA "public" TO "service_role";

























































































































































GRANT ALL ON FUNCTION "public"."handle_new_user_profile"() TO "anon";
GRANT ALL ON FUNCTION "public"."handle_new_user_profile"() TO "authenticated";
GRANT ALL ON FUNCTION "public"."handle_new_user_profile"() TO "service_role";


















GRANT ALL ON TABLE "public"."UserProfiles" TO "anon";
GRANT ALL ON TABLE "public"."UserProfiles" TO "authenticated";
GRANT ALL ON TABLE "public"."UserProfiles" TO "service_role";



GRANT ALL ON TABLE "public"."VaultEntries" TO "anon";
GRANT ALL ON TABLE "public"."VaultEntries" TO "authenticated";
GRANT ALL ON TABLE "public"."VaultEntries" TO "service_role";









ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "service_role";






ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "service_role";






ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "service_role";































