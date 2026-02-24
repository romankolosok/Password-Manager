# Password Manager – Core Library Testing Plan

## Table of Contents

1. [Overview](#1-overview)
2. [Testing Infrastructure & Packages](#2-testing-infrastructure--packages)
3. [Accessibility Changes Required](#3-accessibility-changes-required)
4. [Testing Techniques Summary](#4-testing-techniques-summary)
5. [Test Plan by Component](#5-test-plan-by-component)
   - 5.1 [Models](#51-models)
   - 5.2 [Validators](#52-validators)
   - 5.3 [Services](#53-services)
   - 5.4 [Extensions & Helpers](#54-extensions--helpers)
   - 5.5 [Exception Mapper](#55-exception-mapper)
6. [Decision Tables](#6-decision-tables)
7. [Equivalence Class Partitions](#7-equivalence-class-partitions)
8. [OOP Testing Considerations](#8-oop-testing-considerations)
9. [Additional Recommended Techniques](#9-additional-recommended-techniques)
10. [Coverage Goals & Measurement](#10-coverage-goals--measurement)
11. [Test Organisation & Naming Conventions](#11-test-organisation--naming-conventions)

---

## 1. Overview

This document describes a structured testing plan for the **PasswordManager.Core** library. The plan uses four testing methodologies the author has studied — **unit testing**, **equivalence class testing**, **decision table testing**, and **OOP testing** — and suggests additional techniques that add value to this project.

The core library contains:

| Layer | Components |
|-------|-----------|
| **Models** | `Result`, `Result<T>`, `EncryptedBlob`, `PasswordOptions`, `VaultEntry`, `VaultEntryPayload`, `PasswordPolicy` |
| **Entities** | `UserProfileEntity`, `VaultEntryEntity` |
| **Validators** | `PasswordValidator`, `EmailValidator`, `EncryptionInputValidator`, `DecryptionInputValidator`, `PasswordOptionsValidator`, `EncryptedBlobFormatValidator` |
| **Services (Interfaces)** | `ICryptoService`, `ISessionService`, `IAuthService`, `IVaultService`, `IVaultRepository`, `IUserProfileService`, `IPasswordStrengthChecker`, `IClipboardService` |
| **Services (Implementations)** | `CryptoService`, `SessionService`, `AuthService`, `VaultService`, `UserProfileService`, `VaultRepository`, `ZxcvbnPasswordStrengthChecker` |
| **Extensions** | `VaultEntryExtensions` |
| **Exceptions** | `SupabaseExceptionMapper` |

---

## 2. Testing Infrastructure & Packages

### Already in `PasswordManager.Tests.csproj`

| Package | Purpose |
|---------|---------|
| `xunit` (2.5.3) | Test framework – `[Fact]` and `[Theory]` attributes |
| `xunit.runner.visualstudio` (2.5.3) | IDE / `dotnet test` integration |
| `Microsoft.NET.Test.Sdk` (17.9.0) | Test host |
| `Moq` (4.20.72) | Mocking interfaces (`IVaultRepository`, `ICryptoService`, `ISessionService`, etc.) |
| `FluentAssertions` (8.8.0) | Expressive assertions (`result.Should().BeTrue()`) |
| `coverlet.collector` (6.0.0) | Code coverage collection (XPlat Code Coverage) |
| `JunitXml.TestLogger` (8.0.0) | JUnit XML output for Jenkins |

### Recommended additions

| Package | Purpose | Why |
|---------|---------|-----|
| `coverlet.msbuild` | MSBuild-integrated coverage — allows setting coverage thresholds in CI (`/p:Threshold=80`) | More flexible than the collector; can fail builds on low coverage |
| `ReportGenerator` (global tool) | Converts Cobertura XML to HTML reports for human-readable coverage visualisation | `dotnet tool install -g dotnet-reportgenerator-globaltool` |

> **No other packages are needed.** The existing set (xUnit + Moq + FluentAssertions + Coverlet) is industry-standard for .NET unit testing.

---

## 3. Accessibility Changes Required

Several classes in `PasswordManager.Core` are declared `internal`. To test them directly from `PasswordManager.Tests`, add an `InternalsVisibleTo` entry in the Core project:

```xml
<!-- PasswordManager.Core.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="PasswordManager.Tests" />
</ItemGroup>
```

This exposes the following internals to the test project:

| Internal type | File |
|--------------|------|
| `PasswordPolicy` | Models/PasswordPolicy.cs |
| `VaultEntryPayload` | Models/VaultEntryPayload.cs |
| `VaultEntryExtensions` | Extensions/VaultEntryExtensions.cs |
| `PasswordInput` | Validation/PasswordValidator.cs |
| `PasswordValidator` | Validation/PasswordValidator.cs |
| `EmailInput` | Validation/EmailValidator.cs |
| `EmailValidator` | Validation/EmailValidator.cs |
| `EncryptionInput` | Validation/EncryptionInputValidator.cs |
| `EncryptionInputValidator` | Validation/EncryptionInputValidator.cs |
| `DecryptionInput` | Validation/DecryptionInputValidator.cs |
| `DecryptionInputValidator` | Validation/DecryptionInputValidator.cs |
| `PasswordOptionsValidator` | Validation/PasswordOptionsValidator.cs |
| `EncryptedBlobParseInput` | Validation/EncryptedBlobFormatValidator.cs |
| `EncryptedBlobFormatValidator` | Validation/EncryptedBlobFormatValidator.cs |

---

## 4. Testing Techniques Summary

| Technique | When to use in this project |
|-----------|---------------------------|
| **Unit testing** | Every public/internal method; isolate with mocks for services that have dependencies |
| **Equivalence class (partition) testing** | Validators, `CryptoService.GeneratePassword`, `EncryptedBlob.FromBase64String` — partition inputs into valid/invalid groups and pick representatives |
| **Decision table testing** | `PasswordOptionsValidator` (boolean flags), `CryptoService.GeneratePassword` (flag combinations), `SupabaseExceptionMapper` (status code mapping) |
| **OOP testing** | Test the `IDisposable` contract on `SessionService`, polymorphism through interfaces, `Result`/`Result<T>` inheritance, `BaseModel` inheritance on entities |
| **Boundary value analysis** *(recommended)* | Password lengths at 11/12/128/129, key sizes at 31/32/33, email at 256/257 chars |
| **State-based testing** *(recommended)* | `SessionService` (unlocked → locked → disposed lifecycle) |
| **Pairwise / combinatorial testing** *(recommended)* | `PasswordOptions` has 6 boolean/numeric parameters — combinatorial testing reduces the test-case explosion |

---

## 5. Test Plan by Component

### 5.1 Models

#### 5.1.1 `Result` and `Result<T>`

| # | Test case | Technique | Expected |
|---|-----------|-----------|----------|
| 1 | `Result.Ok()` returns `Success = true`, empty message | Unit | Pass |
| 2 | `Result.Fail("msg")` returns `Success = false`, message = "msg" | Unit | Pass |
| 3 | `Result<int>.Ok(42)` returns `Success = true`, `Value = 42` | Unit | Pass |
| 4 | `Result<int>.Fail("err")` returns `Success = false`, `Value = default(int)` | Unit | Pass |
| 5 | `Result<string>.Fail("err")` returns `Value = null` (default for ref type) | Unit | Pass |
| 6 | `Result<T>` is assignable to `Result` (polymorphism) | OOP | Compiles & correct at runtime |

#### 5.1.2 `EncryptedBlob`

| # | Test case | Technique | Expected |
|---|-----------|-----------|----------|
| 1 | Round-trip: create blob → `ToBase64String()` → `FromBase64String()` recovers same Nonce, Ciphertext, Tag | Unit | Byte arrays match |
| 2 | `FromBase64String(null)` → `Result.Fail` | Equivalence (invalid) | Failure result |
| 3 | `FromBase64String("")` → `Result.Fail` | Equivalence (invalid) | Failure result |
| 4 | `FromBase64String("not-base64!!")` → `Result.Fail` | Equivalence (invalid) | Failure result |
| 5 | `FromBase64String(base64Of27Bytes)` → `Result.Fail` (just under 28) | Boundary | Failure result |
| 6 | `FromBase64String(base64Of28Bytes)` → `Result.Ok` (exact minimum) | Boundary | Success result |
| 7 | `FromBase64String(base64OfLargeBlob)` → `Result.Ok` | Equivalence (valid) | Success result |

#### 5.1.3 `PasswordOptions`

| # | Test case | Technique | Expected |
|---|-----------|-----------|----------|
| 1 | Default values are Length=20, all Include=true, ExcludeAmbiguous=false | Unit | Correct defaults |

#### 5.1.4 `VaultEntry`

| # | Test case | Technique | Expected |
|---|-----------|-----------|----------|
| 1 | Default construction — `Id` is a non-empty Guid, strings default to `string.Empty` | Unit | Pass |
| 2 | Properties can be set and read | Unit | Pass |
| 3 | `Id` is init-only — setting after construction should not compile (verify design) | OOP | Design assertion |

#### 5.1.5 `VaultEntryPayload`

| # | Test case | Technique | Expected |
|---|-----------|-----------|----------|
| 1 | `ToJson()` → `FromJson()` round-trip preserves all fields | Unit | All values match |
| 2 | `FromJson(null)` returns `null` | Equivalence (invalid) | `null` |
| 3 | `FromJson("")` returns `null` | Equivalence (invalid) | `null` |
| 4 | `FromJson("{ invalid json")` returns `null` | Equivalence (invalid) | `null` |
| 5 | `FromJson` with valid JSON but missing optional fields | Equivalence (edge) | Behaviour documented |

#### 5.1.6 `PasswordPolicy`

| # | Test case | Technique | Expected |
|---|-----------|-----------|----------|
| 1 | `MinLength` = 12 | Unit | Constant check |
| 2 | `MaxLength` = 128 | Unit | Constant check |
| 3 | `SpecialCharacters` contains expected characters | Unit | Match expected string |

---

### 5.2 Validators

All validators use FluentValidation; test via `validator.Validate(input)`.

#### 5.2.1 `PasswordValidator`

**Equivalence classes:**

| Class | Representative | Expected |
|-------|---------------|----------|
| Empty / null password | `""`, `null` | Invalid – "cannot be empty" |
| Too short (1–11 chars) | `"Abc1!aaaa"` (9 chars) | Invalid – min length |
| Exactly minimum length (12), all rules met | `"Abcdefgh1!xy"` | Valid |
| Missing uppercase (12+ chars) | `"abcdefgh1!xy"` | Invalid |
| Missing lowercase (12+ chars) | `"ABCDEFGH1!XY"` | Invalid |
| Missing digit (12+ chars) | `"Abcdefghij!x"` | Invalid |
| Missing special character (12+ chars) | `"Abcdefghij1x"` | Invalid |
| Exceeds max length (129 chars) | 129-char string with all types | Invalid |
| Exactly max length (128) | 128-char valid string | Valid |

**Boundary values:** 0, 1, 11, 12, 128, 129 character lengths.

#### 5.2.2 `EmailValidator`

**Equivalence classes:**

| Class | Representative | Expected |
|-------|---------------|----------|
| Empty / null | `""` | Invalid |
| Valid email | `"user@example.com"` | Valid |
| Missing `@` | `"userexample.com"` | Invalid |
| Missing domain | `"user@"` | Invalid |
| Exactly 256 chars | `"a{241}@example.com"` (total 256) | Valid |
| 257 chars | One char longer | Invalid |

#### 5.2.3 `EncryptionInputValidator`

**Equivalence classes:**

| Class | Representative | Expected |
|-------|---------------|----------|
| Valid plaintext + 32-byte key | `("hello", 32-byte key)` | Valid |
| Empty plaintext | `("", 32-byte key)` | Invalid |
| Null plaintext | `(null, 32-byte key)` | Invalid |
| Key = null | `("hello", null)` | Invalid |
| Key = 31 bytes | `("hello", 31-byte key)` | Invalid (boundary) |
| Key = 33 bytes | `("hello", 33-byte key)` | Invalid (boundary) |
| Key = 0 bytes | `("hello", empty byte[])` | Invalid |

#### 5.2.4 `DecryptionInputValidator`

**Equivalence classes:**

| Class | Representative | Expected |
|-------|---------------|----------|
| Valid blob (12-byte nonce, non-empty ciphertext, 16-byte tag) + 32-byte key | Valid inputs | Valid |
| Null blob | `(null, 32-byte key)` | Invalid |
| Nonce wrong size (11 bytes) | Boundary | Invalid |
| Nonce wrong size (13 bytes) | Boundary | Invalid |
| Empty ciphertext | Edge | Invalid |
| Tag wrong size (15 bytes) | Boundary | Invalid |
| Tag wrong size (17 bytes) | Boundary | Invalid |
| Key = 31 bytes | Boundary | Invalid |
| Key = 33 bytes | Boundary | Invalid |

#### 5.2.5 `PasswordOptionsValidator`

**Decision table** (see [Section 6.1](#61-passwordoptionsvalidator-decision-table) below).

**Equivalence classes for Length:**

| Class | Representative | Expected |
|-------|---------------|----------|
| Below minimum (< 12) | 11 | Invalid |
| At minimum | 12 | Valid |
| Within range | 20 | Valid |
| At maximum | 128 | Valid |
| Above maximum (> 128) | 129 | Invalid |
| Zero | 0 | Invalid |
| Negative | -1 | Invalid |

#### 5.2.6 `EncryptedBlobFormatValidator`

| Class | Representative | Expected |
|-------|---------------|----------|
| Empty string | `""` | Invalid |
| Null | `null` | Invalid |
| Non-base64 | `"%%%"` | Invalid |
| Valid base64, < 28 bytes decoded | base64 of 27 bytes | Invalid |
| Valid base64, = 28 bytes decoded | base64 of 28 bytes | Valid (boundary) |
| Valid base64, > 28 bytes decoded | base64 of 100 bytes | Valid |

---

### 5.3 Services

#### 5.3.1 `CryptoService` (test the real implementation — no mocking needed)

**Unit tests:**

| # | Test case | Technique |
|---|-----------|-----------|
| 1 | `DeriveKey` returns 32 bytes for any password + salt | Unit |
| 2 | `DeriveKey` with same password + salt → same key (deterministic) | Unit |
| 3 | `DeriveKey` with different passwords → different keys | Unit |
| 4 | `DeriveKey` with different salts → different keys | Unit |
| 5 | `Encrypt` + `Decrypt` round-trip returns original plaintext | Unit |
| 6 | `Encrypt` with null/empty plaintext → `Result.Fail` | Equivalence |
| 7 | `Encrypt` with wrong key size → `Result.Fail` | Boundary |
| 8 | `Decrypt` with wrong key → `Result.Fail` (authentication failure) | Unit |
| 9 | `Decrypt` with tampered ciphertext → `Result.Fail` | Unit |
| 10 | `Decrypt` with tampered tag → `Result.Fail` | Unit |
| 11 | `Decrypt` with tampered nonce → `Result.Fail` | Unit |
| 12 | `Decrypt` with null blob → `Result.Fail` | Equivalence |
| 13 | Two `Encrypt` calls with same plaintext + key produce different nonces (non-deterministic) | Unit |
| 14 | `GenerateSalt` returns 16 bytes | Unit |
| 15 | `GenerateSalt` returns different values on successive calls | Unit |

**`GeneratePassword` — Decision table tests** (see [Section 6.2](#62-cryptoservicegeneratepassword-decision-table)):

| # | Test case | Technique |
|---|-----------|-----------|
| 16 | Default options → valid password of length 20 containing all char types | Unit |
| 17 | Length = 12 (boundary min) → password of length 12 | Boundary |
| 18 | Length = 128 (boundary max) → password of length 128 | Boundary |
| 19 | Length = 11 → `Result.Fail` | Boundary |
| 20 | Length = 129 → `Result.Fail` | Boundary |
| 21 | Only uppercase → password has only uppercase chars | Decision table |
| 22 | Only lowercase → password has only lowercase chars | Decision table |
| 23 | Only digits → password has only digits | Decision table |
| 24 | Only special chars → password has only special chars | Decision table |
| 25 | No character types selected → `Result.Fail` | Decision table |
| 26 | ExcludeAmbiguous = true → no ambiguous chars (`0OolI1|\`'"`) in result | Unit |
| 27 | All types + ExcludeAmbiguous → generated password still valid | Combinatorial |

#### 5.3.2 `SessionService` (test the real implementation)

**State-based / lifecycle tests:**

| # | Test case | Technique |
|---|-----------|-----------|
| 1 | Newly created → `IsActive()` = false | Unit |
| 2 | After `SetDerivedKey` → `IsActive()` = true | Unit |
| 3 | `GetDerivedKey` before `SetDerivedKey` → throws `InvalidOperationException` | Unit |
| 4 | `GetDerivedKey` returns a clone (modifying returned array doesn't affect session) | Unit |
| 5 | `SetUser` + `CurrentUserId` / `CurrentUserEmail` returns correct values | Unit |
| 6 | `GetAccessToken` returns token set via `SetUser` | Unit |
| 7 | `ClearSession` → `IsActive()` = false, derived key zeroed | Unit |
| 8 | `ClearSession` fires `VaultLocked` event | Unit / OOP (event) |
| 9 | `SetDerivedKey(null)` → `ArgumentNullException` | Equivalence |
| 10 | `SetUser(_, null)` → `ArgumentNullException` | Equivalence |
| 11 | Setting `InactivityTimeout` to zero/negative → `ArgumentOutOfRangeException` | Boundary |
| 12 | Setting `InactivityTimeout` to positive value → succeeds | Boundary |
| 13 | After `Dispose()`, any method call → `ObjectDisposedException` | OOP (IDisposable) |
| 14 | `ClearSession` after `Dispose` does not throw (graceful) | OOP (IDisposable) |
| 15 | Inactivity timer expires → `ClearSession` called → `VaultLocked` fires | State-based |
| 16 | `ResetInactivityTimer` resets the countdown | State-based |
| 17 | Double `SetDerivedKey` clears old key from memory | Unit |

#### 5.3.3 `VaultService` (mock `ICryptoService`, `ISessionService`, `IVaultRepository`, `ILogger<VaultService>`)

| # | Test case | Technique |
|---|-----------|-----------|
| 1 | `GetAllEntriesAsync` when session inactive → `Result.Fail("Vault is locked")` | Unit |
| 2 | `GetAllEntriesAsync` happy path — decrypts all entries | Unit |
| 3 | `GetAllEntriesAsync` skips entries that fail decryption | Unit |
| 4 | `GetEntryAsync` with invalid GUID string → `Result.Fail("Invalid entry id")` | Equivalence |
| 5 | `GetEntryAsync` entry not found → `Result.Fail("Entry not found")` | Unit |
| 6 | `GetEntryAsync` happy path → decrypted entry | Unit |
| 7 | `GetEntryAsync` decryption fails → `Result.Fail("Failed to decrypt entry")` | Unit |
| 8 | `GetEntryAsync` repository throws → `Result.Fail("Failed to retrieve entry")` | Unit |
| 9 | `AddEntryAsync` when session inactive → `Result.Fail("Vault is locked")` | Unit |
| 10 | `AddEntryAsync` when no user → `Result.Fail("No user logged in")` | Unit |
| 11 | `AddEntryAsync` happy path (new entry) — sets new Id, timestamps, encrypts, upserts | Unit |
| 12 | `AddEntryAsync` happy path (existing entry, update) — preserves Id/CreatedAt | Unit |
| 13 | `AddEntryAsync` encryption fails → `Result.Fail` | Unit |
| 14 | `AddEntryAsync` repository throws → `Result.Fail("Failed to save entry")` | Unit |
| 15 | `DeleteEntryAsync` when session inactive → `Result.Fail` | Unit |
| 16 | `DeleteEntryAsync` invalid GUID → `Result.Fail("Invalid entry id")` | Equivalence |
| 17 | `DeleteEntryAsync` happy path → `Result.Ok` | Unit |
| 18 | `DeleteEntryAsync` repository throws → `Result.Fail("Failed to delete entry")` | Unit |

**`SearchEntries` — Pure function, no mocking needed:**

| # | Test case | Technique |
|---|-----------|-----------|
| 19 | Null/empty/whitespace query → returns all entries | Equivalence |
| 20 | Query matches `WebsiteName` (case-insensitive) | Unit |
| 21 | Query matches `Username` | Unit |
| 22 | Query matches `Url` | Unit |
| 23 | Query matches `Notes` | Unit |
| 24 | Query matches `Category` | Unit |
| 25 | Query matches no entries → empty list | Unit |
| 26 | Query with leading/trailing whitespace → still matches | Equivalence |
| 27 | Entries with null fields do not throw | Robustness |

#### 5.3.4 `UserProfileService` (mock `IVaultRepository`)

| # | Test case | Technique |
|---|-----------|-----------|
| 1 | `CreateProfileAsync` happy path → `Result.Ok` | Unit |
| 2 | `CreateProfileAsync` `PostgrestException` → `Result.Fail` with DB error message | Unit |
| 3 | `CreateProfileAsync` generic exception → `Result.Fail` | Unit |
| 4 | `GetProfileAsync` happy path → `Result.Ok` with profile | Unit |
| 5 | `GetProfileAsync` not found (null) → `Result.Fail("User profile not found.")` | Unit |
| 6 | `GetProfileAsync` `PostgrestException` → `Result.Fail` with DB error message | Unit |
| 7 | `GetProfileAsync` generic exception → `Result.Fail` | Unit |

#### 5.3.5 `ZxcvbnPasswordStrengthChecker` (test real implementation)

| # | Test case | Technique |
|---|-----------|-----------|
| 1 | `CheckStrength("")` → score 0 | Equivalence |
| 2 | `CheckStrength("password")` → low score (0 or 1) | Equivalence |
| 3 | `CheckStrength` of a long random password → high score (3 or 4) | Equivalence |
| 4 | `GetStrengthLabel(0)` → "Very Weak" | Unit |
| 5 | `GetStrengthLabel(1)` → "Weak" | Unit |
| 6 | `GetStrengthLabel(2)` → "Fair" | Unit |
| 7 | `GetStrengthLabel(3)` → "Strong" | Unit |
| 8 | `GetStrengthLabel(4)` → "Very Strong" | Unit |
| 9 | `GetStrengthLabel(5)` → "Unknown" | Boundary |
| 10 | `GetStrengthLabel(-1)` → "Unknown" | Boundary |
| 11 | `GetFeedback` returns string (not null) | Unit |

#### 5.3.6 `SupabaseExceptionMapper`

| # | Test case | Technique |
|---|-----------|-----------|
| 1 | `GotrueException` status 422 → "already exists" message | Decision table |
| 2 | `GotrueException` status 400 → "Invalid request" message | Decision table |
| 3 | `GotrueException` status 429 → "Too many attempts" message | Decision table |
| 4 | `GotrueException` other status, message contains "already registered" → "already exists" | Decision table |
| 5 | `GotrueException` other status, message contains "invalid" → "Invalid email or password" | Decision table |
| 6 | `GotrueException` other status, generic message → "Authentication failed" | Decision table |
| 7 | `HttpRequestException` → "Network error" message | Decision table |
| 8 | Other exception type → "unexpected error" message | Decision table |

#### 5.3.7 `AuthService` (mock `Supabase.Client`, `ICryptoService`, `IUserProfileService`, `ISessionService`, `ISupabaseExceptionMapper`, `ILogger`)

> **Note:** `AuthService` has a direct dependency on `Supabase.Client` (concrete class) which makes it harder to mock. Consider testing at a higher level or extracting an `IAuthClient` interface. For now, focus on the logic paths:

| # | Test case | Technique |
|---|-----------|-----------|
| 1 | `RegisterAsync` with invalid email → `Result.Fail` from email validation | Unit |
| 2 | `RegisterAsync` with invalid password → `Result.Fail` from password validation | Unit |
| 3 | `LoginAsync` with invalid session → `Result.Fail` | Unit |
| 4 | `IsLocked` when no Supabase session and no internal session → `true` | Unit |
| 5 | `ChangeMasterPasswordAsync` → `Result.Fail("Not implemented.")` | Unit |

> **Recommendation:** `AuthService` and `VaultRepository` depend on `Supabase.Client` (concrete). These are better suited for **integration tests** in a future phase, unless an abstraction layer is added.

---

### 5.4 Extensions & Helpers

#### 5.4.1 `VaultEntryExtensions`

| # | Test case | Technique |
|---|-----------|-----------|
| 1 | `ToPayload` maps all fields from `VaultEntry` to `VaultEntryPayload` | Unit |
| 2 | `ToPayload` null fields → default to empty string | Equivalence |
| 3 | `ToVaultEntry(Guid, DateTime, DateTime)` maps all fields + metadata | Unit |
| 4 | `ToVaultEntry(VaultEntryEntity)` maps using entity metadata | Unit |
| 5 | Round-trip: `entry.ToPayload().ToVaultEntry(id, created, updated)` preserves values | Unit |

---

### 5.5 Exception Mapper

See [Section 5.3.6](#536-supabaseexceptionmapper) above.

---

## 6. Decision Tables

### 6.1 `PasswordOptionsValidator` Decision Table

The validator checks two conditions: (1) length in range and (2) at least one character set selected.

| Rule | Length in [12, 128]? | ≥1 char type? | Expected |
|------|---------------------|--------------|----------|
| R1 | Yes | Yes | Valid |
| R2 | Yes | No (all false) | Invalid – "At least one character set" |
| R3 | No (< 12) | Yes | Invalid – "length must be between" |
| R4 | No (> 128) | Yes | Invalid – "length must be between" |
| R5 | No | No | Invalid – both errors |

### 6.2 `CryptoService.GeneratePassword` Decision Table

Key boolean flags: `IncludeUppercase (U)`, `IncludeLowercase (L)`, `IncludeDigits (D)`, `IncludeSpecialCharacters (S)`, `ExcludeAmbiguousCharacters (A)`.

| Rule | U | L | D | S | A | Expected |
|------|---|---|---|---|---|----------|
| R1 | T | T | T | T | F | Password contains all four char types |
| R2 | T | T | T | T | T | All four types, no ambiguous chars |
| R3 | T | F | F | F | F | Uppercase only |
| R4 | F | T | F | F | F | Lowercase only |
| R5 | F | F | T | F | F | Digits only |
| R6 | F | F | F | T | F | Special chars only |
| R7 | F | F | F | F | F | Fail – no char types (caught by validator) |
| R8 | T | T | F | F | F | Upper + lower only |
| R9 | T | F | T | F | T | Upper + digits, no ambiguous |
| R10 | F | T | F | T | T | Lower + special, no ambiguous |

### 6.3 `SupabaseExceptionMapper` Decision Table

| Rule | Exception type | Status code | Message contains | Result message |
|------|---------------|-------------|-----------------|----------------|
| R1 | GotrueException | 422 | (any) | "already exists" |
| R2 | GotrueException | 400 | (any) | "Invalid request" |
| R3 | GotrueException | 429 | (any) | "Too many attempts" |
| R4 | GotrueException | other | "already registered" | "already exists" |
| R5 | GotrueException | other | "invalid" | "Invalid email or password" |
| R6 | GotrueException | other | (other) | "Authentication failed" |
| R7 | HttpRequestException | — | — | "Network error" |
| R8 | (other) | — | — | "unexpected error" |

---

## 7. Equivalence Class Partitions

### 7.1 Password Validation

| Partition | Representative values |
|-----------|---------------------|
| **Valid** | `"Abcdefgh1!xy"` (12 chars, all types) |
| **Empty** | `""`, `null` |
| **Too short** | `"Ab1!"` (4 chars) |
| **Too long** | 129-char string |
| **Missing uppercase** | `"abcdefgh1!xy"` |
| **Missing lowercase** | `"ABCDEFGH1!XY"` |
| **Missing digit** | `"Abcdefghij!x"` |
| **Missing special** | `"Abcdefghij1x"` |

### 7.2 Email Validation

| Partition | Representative values |
|-----------|---------------------|
| **Valid** | `"user@example.com"`, `"a@b.co"` |
| **Empty** | `""`, `null` |
| **No @** | `"userexample.com"` |
| **No domain** | `"user@"` |
| **Too long** | 257-char email |

### 7.3 Encryption Key

| Partition | Representative values |
|-----------|---------------------|
| **Valid** | 32-byte array filled with random data |
| **Null** | `null` |
| **Too short** | 31-byte array |
| **Too long** | 33-byte array |
| **Empty** | 0-byte array |

### 7.4 Base64 Encrypted Blob

| Partition | Representative values |
|-----------|---------------------|
| **Valid (28+ bytes)** | Base64 of 28-byte, 100-byte arrays |
| **Empty** | `""` |
| **Null** | `null` |
| **Invalid base64** | `"%%%notbase64"` |
| **Valid base64, too short** | Base64 of 27-byte array |

---

## 8. OOP Testing Considerations

### 8.1 Inheritance Testing

- **`Result<T>` extends `Result`**: Verify that `Result<T>` instances can be assigned to `Result` variables and that `Success`/`Message` are accessible through the base type.
- **`VaultEntryEntity` / `UserProfileEntity` extend `BaseModel`**: Verify they can be used polymorphically where `BaseModel` is expected.

### 8.2 Interface Contract Testing

For each interface implementation, verify:

| Interface | Implementation | Key contract to verify |
|-----------|---------------|----------------------|
| `ICryptoService` | `CryptoService` | Encrypt→Decrypt round-trip; DeriveKey determinism |
| `ISessionService` | `SessionService` | Lifecycle: Set→Get→Clear→Get throws |
| `IPasswordStrengthChecker` | `ZxcvbnPasswordStrengthChecker` | Score is 0–4; labels match |
| `ISupabaseExceptionMapper` | `SupabaseExceptionMapper` | All exception types mapped |
| `IDisposable` | `SessionService` | Dispose clears state; double-dispose safe |

### 8.3 Dispose Pattern Testing (`SessionService`)

| # | Test case |
|---|-----------|
| 1 | After `Dispose()`, `GetDerivedKey()` → `ObjectDisposedException` |
| 2 | After `Dispose()`, `SetDerivedKey(key)` → `ObjectDisposedException` |
| 3 | After `Dispose()`, `SetUser(...)` → `ObjectDisposedException` |
| 4 | After `Dispose()`, `IsActive()` → `ObjectDisposedException` |
| 5 | After `Dispose()`, `ResetInactivityTimer()` → `ObjectDisposedException` |
| 6 | After `Dispose()`, `ClearSession()` → does not throw (graceful) |
| 7 | Double `Dispose()` → does not throw |
| 8 | `InactivityTimeout` getter/setter after `Dispose()` → `ObjectDisposedException` |

### 8.4 Encapsulation Testing

- **`SessionService.GetDerivedKey()` returns a clone**: Modify the returned array → verify internal state is unchanged.
- **`EncryptedBlob`**: Verify that modifying `Nonce`/`Ciphertext`/`Tag` arrays after `Encrypt` does not affect internal copies (if applicable).

---

## 9. Additional Recommended Techniques

### 9.1 Boundary Value Analysis (BVA)

Already incorporated above. Key boundaries:

| Parameter | Min | Min-1 | Max | Max+1 |
|-----------|-----|-------|-----|-------|
| Password length | 12 | 11 | 128 | 129 |
| Encryption key size | 32 | 31 | 32 | 33 |
| Email length | 1 | 0 | 256 | 257 |
| Nonce size | 12 | 11 | 12 | 13 |
| Tag size | 16 | 15 | 16 | 17 |
| Generated password length | 12 | 11 | 128 | 129 |

### 9.2 State Transition Testing

`SessionService` has clear states:

```
[Created] --SetDerivedKey()--> [Active] --ClearSession()--> [Locked]
                                  |                            |
                                  |--Dispose()-->  [Disposed]  |--SetDerivedKey()--> [Active]
                                                       |
                                  [Locked] --Dispose()--> [Disposed]
```

Test each transition and verify that invalid transitions either throw or are handled gracefully.

### 9.3 Pairwise / Combinatorial Testing

`PasswordOptions` has 5 boolean flags + 1 numeric parameter = a large combination space. Use pairwise testing to reduce the number of test cases while covering all two-way interactions. Tools like PICT or manual pair selection can help.

Example pairwise set for the boolean flags:

| U | L | D | S | A |
|---|---|---|---|---|
| T | T | T | T | F |
| T | F | F | F | T |
| F | T | F | T | F |
| F | F | T | F | T |
| T | T | F | F | F |
| F | F | F | T | T |

### 9.4 Error-Guessing

Based on the codebase, common areas to probe:

| Area | Error guess |
|------|------------|
| `CryptoService.DeriveKey` | Empty password, very long password, unicode password |
| `CryptoService.Encrypt` | Empty string, very large plaintext, unicode plaintext |
| `EncryptedBlob.ToBase64String` | Blob with empty ciphertext (edge: 0-byte ciphertext) |
| `VaultService.SearchEntries` | Entries with null properties, empty entries list |
| `SessionService` | Concurrent calls from multiple threads |

### 9.5 Regression Testing

After fixing any bug, add a test that specifically reproduces the bug to prevent regression. Tag these tests with `[Trait("Category", "Regression")]`.

---

## 10. Coverage Goals & Measurement

### 10.1 Running Coverage

```bash
# Run tests with coverage collection
dotnet test PasswordManager.Tests/PasswordManager.Tests.csproj \
  --collect:"XPlat Code Coverage"

# Generate HTML report (after installing ReportGenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html
```

### 10.2 Coverage Targets

| Metric | Target | Notes |
|--------|--------|-------|
| **Line coverage** | ≥ 80% | Across all Core library classes |
| **Branch coverage** | ≥ 75% | Important for validators and decision logic |
| **Method coverage** | 100% | Every public/internal method should have at least one test |
| **Class coverage** | 100% | Every class should have a corresponding test class |

### 10.3 Coverage Exclusions

The following should be excluded from coverage metrics (if desired):
- `VaultRepository` (Supabase-dependent; integration test candidate)
- `AuthService` (Supabase-dependent; integration test candidate)

### 10.4 Enforcing Coverage in CI

Add to the Jenkins `dotnet test` command:

```bash
dotnet test PasswordManager.Tests/PasswordManager.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
```

With `coverlet.msbuild`, you can enforce thresholds:

```bash
dotnet test /p:CollectCoverage=true /p:Threshold=80 /p:ThresholdType=line
```

---

## 11. Test Organisation & Naming Conventions

### 11.1 File / Class Structure

Mirror the source project structure:

```
PasswordManager.Tests/
├── Models/
│   ├── ResultTests.cs
│   ├── EncryptedBlobTests.cs
│   ├── PasswordOptionsTests.cs
│   ├── VaultEntryTests.cs
│   ├── VaultEntryPayloadTests.cs
│   └── PasswordPolicyTests.cs
├── Validation/
│   ├── PasswordValidatorTests.cs
│   ├── EmailValidatorTests.cs
│   ├── EncryptionInputValidatorTests.cs
│   ├── DecryptionInputValidatorTests.cs
│   ├── PasswordOptionsValidatorTests.cs
│   └── EncryptedBlobFormatValidatorTests.cs
├── Services/
│   ├── CryptoServiceTests.cs
│   ├── SessionServiceTests.cs
│   ├── VaultServiceTests.cs
│   ├── UserProfileServiceTests.cs
│   └── ZxcvbnPasswordStrengthCheckerTests.cs
├── Extensions/
│   └── VaultEntryExtensionsTests.cs
└── Exceptions/
    └── SupabaseExceptionMapperTests.cs
```

### 11.2 Naming Convention

Use the pattern: **`MethodName_Scenario_ExpectedBehavior`**

Examples:
- `Encrypt_ValidPlaintextAndKey_ReturnsSuccessResult`
- `Encrypt_EmptyPlaintext_ReturnsFailResult`
- `DeriveKey_SamePasswordAndSalt_ReturnsSameKey`
- `Validate_PasswordTooShort_ReturnsInvalid`
- `SearchEntries_EmptyQuery_ReturnsAllEntries`

### 11.3 Test Attributes

- `[Fact]` — single deterministic test case
- `[Theory]` + `[InlineData(...)]` — parameterised tests (ideal for equivalence classes)
- `[Trait("Category", "UnitTest")]` — categorisation
- `[Trait("Category", "BoundaryValue")]` — tag boundary tests
- `[Trait("Category", "DecisionTable")]` — tag decision table tests

### 11.4 Arrange-Act-Assert (AAA)

Every test should follow the AAA pattern:

```
// Arrange — set up inputs, mocks, SUT
// Act — call the method under test
// Assert — verify the result
```

---

## Summary of Test Cases by Technique

| Technique | Approximate count |
|-----------|------------------|
| Unit tests (standard) | ~65 |
| Equivalence class tests | ~35 |
| Boundary value tests | ~20 |
| Decision table tests | ~18 |
| OOP / lifecycle tests | ~15 |
| State transition tests | ~6 |
| Error-guessing tests | ~8 |
| **Total** | **~167** |

This count is an estimate. Some test cases serve multiple techniques (e.g., a boundary value test is also a unit test). The goal is comprehensive coverage of the core library using the techniques described above.
