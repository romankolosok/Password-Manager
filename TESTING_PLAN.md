# Password Manager – Core Library Testing Plan (Updated)

## Table of Contents

1. [Overview and Scope](#1-overview-and-scope)
2. [Testing Infrastructure and Packages](#2-testing-infrastructure-and-packages)
3. [Accessibility Changes Required](#3-accessibility-changes-required)
4. [Testing Techniques Glossary and When to Use Each](#4-testing-techniques-glossary-and-when-to-use-each)
5. [Detailed Test Plan by Component](#5-detailed-test-plan-by-component)
   - 5.1 [Models](#51-models)
   - 5.2 [Validators](#52-validators)
   - 5.3 [Services (Unit-Testable)](#53-services-unit-testable)
   - 5.4 [Services (Integration — Local Supabase)](#54-services-integration--local-supabase)
   - 5.5 [Extensions](#55-extensions)
   - 5.6 [Exception Mapper](#56-exception-mapper)
6. [Decision Tables](#6-decision-tables)
7. [Equivalence Class Partitions](#7-equivalence-class-partitions)
8. [OOP Testing Considerations](#8-oop-testing-considerations)
9. [Additional Recommended Techniques](#9-additional-recommended-techniques)
10. [Coverage Goals and Measurement](#10-coverage-goals-and-measurement)
11. [Test Organisation and Naming Conventions](#11-test-organisation-and-naming-conventions)

---

## 1. Overview and Scope

This document provides a **detailed, per-component and per-method** testing strategy for the **PasswordManager.Core** library. For every class and method, we explain:

- **What to mock** and why
- **What fixture to create** (shared setup via `IClassFixture<T>`, per-test helper, or integration fixture)
- **Which testing technique** applies (unit test, equivalence class testing, decision table testing, boundary value analysis, state-based testing, slice testing, integration testing) and **why it is the most appropriate** for that specific method

### Key Changes Since Previous Plan

1. **`PasswordGenerator` extracted** — `GeneratePassword` was moved from `CryptoService` into a new `PasswordGenerator` class implementing `IPasswordGenerator`. `CryptoService` no longer has password generation.
2. **Local Supabase via Docker** — A local Supabase stack (`supabase/config.toml`, migrations) now runs in Docker for CI and local dev. This enables **real integration tests** for `AuthService` and `VaultRepository`.
3. **Custom email regex** — `EmailValidator` now uses a custom `Regex` instead of FluentValidation's built-in `EmailAddress()`.
4. **`VaultEntryPayload` refactored to `required` init properties** — No longer a positional record constructor.
5. **`ZxcvbnPasswordStrengthChecker` excluded from coverage** — Marked `[ExcludeFromCodeCoverage]` since it wraps a third-party library.
6. **`Bogus` added for test data** — `TestData` class provides deterministic fake values.
7. **Existing test infrastructure** — `CryptoServiceFixture`, `EncryptedBlobFixture`, `VaultServiceFixture` already created.

### Core Library Inventory

| Layer | Classes | Count |
|-------|---------|-------|
| **Models** | `Result`, `Result<T>`, `EncryptedBlob`, `PasswordOptions`, `VaultEntry`, `VaultEntryPayload`, `PasswordPolicy` | 7 |
| **Entities** | `UserProfileEntity`, `VaultEntryEntity` | 2 |
| **Validators** | `PasswordValidator`, `EmailValidator`, `EncryptionInputValidator`, `DecryptionInputValidator`, `PasswordOptionsValidator`, `EncryptedBlobFormatValidator` | 6 |
| **Services (Interfaces)** | `ICryptoService`, `IPasswordGenerator`, `ISessionService`, `IAuthService`, `IVaultService`, `IVaultRepository`, `IUserProfileService`, `IPasswordStrengthChecker`, `IClipboardService` | 9 |
| **Services (Implementations)** | `CryptoService`, `PasswordGenerator`, `SessionService`, `AuthService`, `VaultService`, `UserProfileService`, `VaultRepository`, `ZxcvbnPasswordStrengthChecker` | 8 |
| **Extensions** | `VaultEntryExtensions` | 1 |
| **Exceptions** | `SupabaseExceptionMapper` | 1 |

---

## 2. Testing Infrastructure and Packages

### In `PasswordManager.Tests.csproj`

| Package | Version | Purpose |
|---------|---------|---------|
| `xunit` | 2.5.3 | Test framework (`[Fact]`, `[Theory]`) |
| `xunit.runner.visualstudio` | 2.5.3 | IDE / `dotnet test` integration |
| `Microsoft.NET.Test.Sdk` | 17.9.0 | Test host |
| `Moq` | 4.20.72 | Mocking interfaces for isolation |
| `FluentAssertions` | 8.8.0 | Expressive assertions |
| `coverlet.msbuild` | 6.0.0 | MSBuild-integrated coverage with thresholds |
| `JunitXml.TestLogger` | 8.0.0 | JUnit XML output for Jenkins |
| `Bogus` | 35.6.5 | Deterministic fake data generation |

### Test Data Infrastructure

**`TestData` (static class)** — Central source of deterministic fake values (seeded with `Seed = 121`). Provides `Email()`, `Username()`, `Password()`, `AccessToken()`, `WebsiteName()`, `Url()`, `Notes()`, `Category()`, `DerivedKey()`, `UserId()`. All tests should use `TestData` for consistency.

### Local Supabase Stack

For integration tests (`AuthService`, `VaultRepository`), a local Supabase runs in Docker:
- **`supabase/config.toml`** — Project config with `enable_confirmations = false` (instant signup without email verification)
- **`supabase/migrations/`** — Schema with `UserProfiles`, `VaultEntries` tables, RLS policies, and `handle_new_user_profile` trigger
- **CI workflow** — Jenkins runs `supabase start`, `supabase db reset`, exports `Supabase__Url`, `Supabase__AnonKey`, `Supabase__ServiceRoleKey` environment variables

---

## 3. Accessibility Changes Required

The Core project has:

```xml
<!-- PasswordManager.Core.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="PasswordManager.App" />
  <InternalsVisibleTo Include="PasswordManager.Tests" />
</ItemGroup>
```

This exposes to the test project:

| Internal Type | File |
|--------------|------|
| `PasswordPolicy` | Models/PasswordPolicy.cs |
| `VaultEntryPayload` | Models/VaultEntryPayload.cs |
| `VaultEntryExtensions` | Extensions/VaultEntryExtensions.cs |
| `PasswordInput`, `PasswordValidator` | Validation/PasswordValidator.cs |
| `EmailInput`, `EmailValidator` | Validation/EmailValidator.cs |
| `EncryptionInput`, `EncryptionInputValidator` | Validation/EncryptionInputValidator.cs |
| `DecryptionInput`, `DecryptionInputValidator` | Validation/DecryptionInputValidator.cs |
| `PasswordOptionsValidator` | Validation/PasswordOptionsValidator.cs |
| `EncryptedBlobParseInput`, `EncryptedBlobFormatValidator` | Validation/EncryptedBlobFormatValidator.cs |

---

## 4. Testing Techniques Glossary and When to Use Each

| Technique | Description | When to Use |
|-----------|-------------|-------------|
| **Unit Testing** | Test a single method in isolation; mock all external dependencies | Every method in the library — the foundation of the strategy |
| **Equivalence Class Testing (ECT)** | Partition inputs into classes of values that should be treated identically, then test one representative from each class | Validators (password, email, key sizes), inputs with clear valid/invalid domains |
| **Boundary Value Analysis (BVA)** | Test at the exact edges of equivalence classes (min, min-1, max, max+1) | Numeric constraints (password length 12/128, key size 32, email length 256) |
| **Decision Table Testing** | Enumerate all combinations of boolean conditions and their expected outcomes | `PasswordOptions` (5 boolean flags), `SupabaseExceptionMapper` (exception type x status code), `PasswordGenerator.Generate` (flag combinations) |
| **State-Based Testing** | Test object lifecycle through state transitions (Created -> Active -> Locked -> Disposed) | `SessionService` — the only stateful service with clear lifecycle states |
| **Slice Testing** | Test a vertical slice through multiple layers without mocking to verify integration | `CryptoService.Encrypt` -> `EncryptedBlob.ToBase64String` -> `FromBase64String` -> `CryptoService.Decrypt` round-trip |
| **Integration Testing** | Test against a real external system (local Supabase Docker) | `AuthService` (real Supabase Auth), `VaultRepository` (real Supabase Postgrest) |
| **OOP Testing** | Test inheritance hierarchies, interface contracts, polymorphism, `IDisposable` pattern | `Result`/`Result<T>` inheritance, `SessionService` `IDisposable`, entity `BaseModel` inheritance |

---

## 5. Detailed Test Plan by Component

---

### 5.1 Models

---

#### 5.1.1 `Result` / `Result<T>`

**File:** `Models/Result.cs`

**What to mock:** Nothing — pure data class with factory methods, no dependencies.

**Fixture:** None needed — each test creates its own `Result` via the static factories. Tests are independent and cheap.

**Why no fixture:** `Result` is a simple immutable model. Construction is trivial and there is no shared state to set up.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| `Result.Ok()` | **Unit test** | Simple factory. Just verify `Success = true` and `Message = ""`. No input variations to partition. |
| `Result.Fail(string)` | **Unit test + Equivalence class** | Test one representative from each class: a normal message, an empty string `""`, and a very long message. The method is a simple factory, but the message parameter has distinct input classes. |
| `Result<T>.Ok(T)` | **Unit test** | Verify `Success = true`, `Value` equals the passed data, `Message = ""`. Use several types for `T` (int, string, complex object) to verify generic behaviour. |
| `Result<T>.Fail(string)` | **Unit test** | Verify `Success = false`, `Value = default(T)`, `Message` set correctly. Test with value types (int -> 0) and reference types (string -> null) to cover both default behaviours. |
| Polymorphism: `Result<T>` is-a `Result` | **OOP test** | Assign `Result<int>` to a `Result` variable and verify `Success`/`Message` are accessible. This tests that the inheritance is working correctly at runtime. |

---

#### 5.1.2 `EncryptedBlob`

**File:** `Models/EncryptedBlob.cs`

**What to mock:** Nothing — `EncryptedBlob` is a model class. Internally it creates an `EncryptedBlobFormatValidator`, but that is part of the unit under test.

**Fixture:** **`EncryptedBlobFixture` (already exists as `IClassFixture<EncryptedBlobFixture>`).**

**Why a fixture:** The fixture pre-constructs several blob scenarios (standard blob, minimum valid base64, large valid base64, invalid base64, boundary 27-byte blob) that are reused across multiple test methods. This avoids repetitive setup in each test and keeps test code focused on assertions.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| `ToBase64String()` | **Unit test** | Verify that concatenation of Nonce + Ciphertext + Tag is correctly base64-encoded. Deterministic output for deterministic input. Uses fixture's `StandardBlob`. |
| `FromBase64String(string)` | **Equivalence class + Boundary value** | The input string falls into distinct classes: (1) null, (2) empty, (3) non-base64 garbage, (4) valid base64 but too short (< 28 bytes decoded), (5) valid base64 exactly 28 bytes (boundary — nonce + tag, zero-length ciphertext), (6) valid base64 with ciphertext. Each class has different expected behaviour. Boundary at 27 vs 28 bytes is critical. Uses fixture's pre-built test cases. |
| Round-trip: `ToBase64String` -> `FromBase64String` | **Slice test** | This is a vertical slice through two methods. Construct a blob, serialise, deserialise, verify all three arrays match. Catches encoding/decoding inconsistencies that per-method tests might miss. |

---

#### 5.1.3 `PasswordOptions`

**File:** `Models/PasswordOptions.cs`

**What to mock:** Nothing — pure data class (POCO).

**Fixture:** None.

**Why no fixture:** Trivial construction, no shared state.

##### Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| Default constructor | **Unit test** | Verify defaults: `Length = 20`, `IncludeUppercase = true`, `IncludeLowercase = true`, `IncludeDigits = true`, `IncludeSpecialCharacters = true`, `ExcludeAmbiguousCharacters = false`. Single deterministic check. |
| Property setters | **Unit test** | Verify each property can be set and read back. |

---

#### 5.1.4 `VaultEntry`

**File:** `Models/VaultEntry.cs`

**What to mock:** Nothing — pure data class.

**Fixture:** None.

##### Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| Default constructor | **Unit test** | Verify `Id` is a non-empty Guid, strings default to `string.Empty`, `IsFavorite = false`. |
| `Id` is init-only | **OOP test** | Verify immutability — `Id` uses `init` accessor. |

---

#### 5.1.5 `VaultEntryPayload` (internal record)

**File:** `Models/VaultEntryPayload.cs`

**What to mock:** Nothing — pure record type with JSON serialisation.

**Fixture:** None.

**Why this needs testing:** It is the data contract for encrypted storage. Incorrect serialisation means data loss. Note: `VaultEntryPayload` was refactored from a positional record to use `required init` properties.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| `ToJson()` | **Unit test** | Create a known payload, serialise, verify JSON contains expected fields. Deterministic. |
| `FromJson(string)` | **Equivalence class** | Input classes: (1) valid JSON matching the record shape, (2) `null`, (3) empty string `""`, (4) invalid JSON `"{ broken"`, (5) valid JSON but wrong shape (missing fields). Each class triggers a different code path (success vs `catch -> null`). |
| Round-trip: `ToJson()` -> `FromJson()` | **Slice test** | Serialise then deserialise. Verify all 7 fields survive the round-trip. Catches JSON property naming mismatches. |

---

#### 5.1.6 `PasswordPolicy` (internal static class)

**File:** `Models/PasswordPolicy.cs`

**What to mock:** Nothing — constants only.

**Fixture:** None.

##### Strategy

| Test | Technique | Reasoning |
|------|-----------|-----------|
| Constants match expected values | **Unit test** | Verify `MinLength = 12`, `MaxLength = 128`, `SpecialCharacters` is the expected string. These are the contract other validators depend on. |

---

### 5.2 Validators

All validators use FluentValidation's `AbstractValidator<T>`. They are pure logic with no external dependencies.

**General approach for all validators:**
- **What to mock:** Nothing — validators are pure functions (input -> validation result).
- **Fixture:** None — validators are cheap to construct.
- **Primary technique:** Equivalence class testing + boundary value analysis, because validators are classifiers — they partition the input domain into "valid" and "invalid" regions, making ECT the natural fit.
- **Why ECT is the best fit for validators:** A validator's job is literally to classify inputs. Equivalence classes map directly onto the validation rules. Each rule defines a partition boundary.

---

#### 5.2.1 `PasswordValidator`

**File:** `Validation/PasswordValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning for strategy choice:** The password validator has 7 rules (not-empty, min length, max length, has uppercase, has lowercase, has digit, has special char). Some rules have `.When()` conditions (character type rules only fire when length >= MinLength). This creates a rich input domain best covered by **equivalence class testing** for the main partitions and **boundary value analysis** for the length constraints.

Note: Private helper methods (`ContainsUppercase`, `ContainsLowercase`, `ContainsDigit`, `ContainsSpecialCharacter`) are marked `[ExcludeFromCodeCoverage]` since they are simple LINQ one-liners.

##### Method: `Validate(PasswordInput)`

**Equivalence classes:**

| # | Class | Representative | Expected | Why this class |
|---|-------|---------------|----------|----------------|
| 1 | Empty / null | `""` | Invalid - "cannot be empty" | First rule fires |
| 2 | Too short (1-11 chars), all types present | `"Ab1!cdefgh"` (10 chars) | Invalid - min length | `.When()` prevents character-type rules from firing |
| 3 | At minimum length (12), all types present | `"Abcdefgh1!xy"` | Valid | Exact boundary of the min-length rule |
| 4 | Missing uppercase (12+ chars) | `"abcdefgh1!xy"` | Invalid - "must contain uppercase" | Character-type rule fires |
| 5 | Missing lowercase (12+ chars) | `"ABCDEFGH1!XY"` | Invalid - "must contain lowercase" | Same reasoning |
| 6 | Missing digit (12+ chars) | `"Abcdefghij!x"` | Invalid - "must contain digit" | Same reasoning |
| 7 | Missing special char (12+ chars) | `"Abcdefghij1x"` | Invalid - "must contain special" | Same reasoning |
| 8 | At max length (128), all types present | 128-char valid string | Valid | Exact boundary |
| 9 | Over max length (129) | 129-char string | Invalid - "must not exceed 128" | Above boundary |

**Boundary values for length:** `0`, `1`, `11`, `12`, `128`, `129`

---

#### 5.2.2 `EmailValidator`

**File:** `Validation/EmailValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** Like `PasswordValidator`, this is a classifier with 3 rules: not-empty, max-length, and now a **custom regex** (instead of FluentValidation's built-in `EmailAddress()`). The custom regex `^[a-z0-9!#$%&'*+/=?^_`{|}~-]+...` is case-sensitive (lowercase only in pattern). ECT + BVA covers it well.

##### Method: `Validate(EmailInput)`

**Equivalence classes:**

| # | Class | Representative | Expected | Why this class |
|---|-------|---------------|----------|----------------|
| 1 | Empty / null | `""` | Invalid - "cannot be empty" | First rule |
| 2 | Valid standard email | `"user@example.com"` | Valid | Happy path |
| 3 | Valid minimal email | `"a@b.co"` | Valid | Shortest plausible valid email |
| 4 | Missing `@` | `"userexample.com"` | Invalid - format | Regex rule |
| 5 | Missing domain | `"user@"` | Invalid - format | Regex rule |
| 6 | At max length (256 chars) | 256-char valid email | Valid | Max boundary |
| 7 | Over max length (257 chars) | 257-char email | Invalid - exceeds 256 | Above boundary |
| 8 | Uppercase letters in local part | `"User@example.com"` | Invalid - regex is lowercase-only | New custom regex behaviour |
| 9 | Double dot in local part | `"user..name@example.com"` | Invalid - regex disallows | Regex edge case |

**Boundary values for length:** `0`, `1`, `256`, `257`

---

#### 5.2.3 `EncryptionInputValidator`

**File:** `Validation/EncryptionInputValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** Two independent rules (plaintext not-empty, key exactly 32 bytes). ECT per-condition is sufficient.

##### Method: `Validate(EncryptionInput)`

| # | Class | Representative | Expected | Why |
|---|-------|---------------|----------|-----|
| 1 | Valid: non-empty plaintext + 32-byte key | `("hello", new byte[32])` | Valid | Happy path |
| 2 | Empty plaintext | `("", new byte[32])` | Invalid | Empty string partition |
| 3 | Null key | `("hello", null)` | Invalid | Null partition |
| 4 | Key too short (31 bytes) | `("hello", new byte[31])` | Invalid | Boundary -1 |
| 5 | Key too long (33 bytes) | `("hello", new byte[33])` | Invalid | Boundary +1 |
| 6 | Key empty (0 bytes) | `("hello", new byte[0])` | Invalid | Edge |

---

#### 5.2.4 `DecryptionInputValidator`

**File:** `Validation/DecryptionInputValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** More conditions (blob not null, nonce 12 bytes, ciphertext not empty, tag 16 bytes, key 32 bytes), but still independent. ECT per-condition + BVA at byte sizes.

##### Method: `Validate(DecryptionInput)`

| # | Class | Expected | Why |
|---|-------|----------|-----|
| 1 | All valid (12-byte nonce, non-empty ciphertext, 16-byte tag, 32-byte key) | Valid | Happy path |
| 2 | Null blob | Invalid | Null partition |
| 3 | Nonce = 11 bytes | Invalid | BVA: min - 1 |
| 4 | Nonce = 13 bytes | Invalid | BVA: min + 1 |
| 5 | Empty ciphertext | Invalid | Empty partition |
| 6 | Tag = 15 bytes | Invalid | BVA: min - 1 |
| 7 | Tag = 17 bytes | Invalid | BVA: min + 1 |
| 8 | Key = 31 bytes | Invalid | BVA: min - 1 |
| 9 | Key = 33 bytes | Invalid | BVA: min + 1 |

---

#### 5.2.5 `PasswordOptionsValidator`

**File:** `Validation/PasswordOptionsValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** 2 rules: (1) `Length` in `[12, 128]` and (2) at least one character set selected. The character set check is a boolean OR of 4 flags — a natural **decision table**.

##### Method: `Validate(PasswordOptions)`

**Decision table** (see [Section 6.1](#61-passwordoptionsvalidator)):

| Rule | Length valid? | >= 1 char type? | Expected |
|------|-------------|---------------|----------|
| R1 | Yes | Yes | Valid |
| R2 | Yes | No (all false) | Invalid |
| R3 | No (< 12) | Yes | Invalid |
| R4 | No (> 128) | Yes | Invalid |
| R5 | No | No | Invalid - both errors |

**Length boundary values:** `11` (below min), `12` (at min), `128` (at max), `129` (above max), `0`, `-1`

---

#### 5.2.6 `EncryptedBlobFormatValidator`

**File:** `Validation/EncryptedBlobFormatValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

##### Method: `Validate(EncryptedBlobParseInput)`

| # | Class | Representative | Expected | Why |
|---|-------|---------------|----------|-----|
| 1 | Empty string | `""` | Invalid | Empty partition |
| 2 | Null | `null` | Invalid | Null partition |
| 3 | Non-base64 garbage | `"%%%not-base64"` | Invalid | Format partition |
| 4 | Valid base64, < 28 bytes decoded | base64 of 27-byte array | Invalid | Boundary - 1 |
| 5 | Valid base64, = 28 bytes decoded | base64 of 28-byte array | Valid | Boundary (minimum valid) |
| 6 | Valid base64, > 28 bytes decoded | base64 of 100-byte array | Valid | Normal valid partition |

---

### 5.3 Services (Unit-Testable)

---

#### 5.3.1 `CryptoService`

**File:** `Services/Implementations/CryptoService.cs`

**What to mock:** Nothing — `CryptoService` is self-contained. It uses real cryptographic algorithms (Argon2id, AES-256-GCM) and internal validators.

**Why no mocking:** Mocking crypto would defeat the purpose — we need to verify real encryption/decryption works. Validators are internal collaborators, not external dependencies.

**Fixture: `CryptoServiceFixture` (already exists as `IClassFixture<CryptoServiceFixture>`)**

**Why a fixture:** `DeriveKey` is computationally expensive (Argon2 with 64 MB memory, ~500ms). The fixture provides:
- A shared `CryptoService` instance
- A pre-derived 32-byte key (from `KnownPassword` + random `Salt`)
- Saves ~500ms per test that needs an encryption key

**Why stateless service benefits from a fixture:** Even though `CryptoService` has no mutable state, the Argon2 key derivation is expensive. A fixture amortises this cost across all tests.

**Note:** `GeneratePassword` was removed from `CryptoService` and moved to `PasswordGenerator`. `CryptoService` now has 4 methods.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`DeriveKey(string, byte[])`** | **Unit test** | (1) Returns exactly 32 bytes. (2) Same password + salt -> same key (determinism). (3) Different passwords -> different keys. (4) Different salts -> different keys. Functional properties of a deterministic algorithm. |
| **`Encrypt(string, byte[])`** | **Equivalence class + Unit test** | Inputs partition into: (1) valid plaintext + valid key -> success with blob, (2) empty/null plaintext -> fail (via validator), (3) wrong key size -> fail (via validator). Also verify randomness: two encryptions of the same plaintext produce different nonces. |
| **`Decrypt(EncryptedBlob, byte[])`** | **Equivalence class + Unit test** | Inputs partition into: (1) correct blob + correct key -> success, (2) wrong key -> fail (AES-GCM auth failure), (3) tampered ciphertext -> fail, (4) tampered tag -> fail, (5) tampered nonce -> fail, (6) null blob -> fail (via validator), (7) wrong key size -> fail. |
| **`Encrypt` -> `Decrypt` round-trip** | **Slice test** | The most important test. Encrypt plaintext -> get blob -> decrypt blob -> get plaintext back. Catches nonce/tag ordering issues. |
| **`GenerateSalt()`** | **Unit test** | Verify: (1) returns 16 bytes, (2) two calls produce different values (randomness). |

---

#### 5.3.2 `PasswordGenerator`

**File:** `Services/Implementations/PasswordGenerator.cs`

**What to mock:** Nothing — self-contained. Uses `PasswordOptionsValidator` internally (tested as part of the unit).

**Fixture:** None needed — construction is trivial (just a validator instantiation).

**Why no fixture:** `PasswordGenerator` is stateless and cheap to construct. Unlike `CryptoService`, there is no expensive one-time setup.

**Reasoning for strategy:** The method takes `PasswordOptions` with 5 booleans + 1 int. This is a combinatorial input space. A **decision table** is the right tool for the boolean flags. **BVA** applies to the length parameter. **ECT** applies to the ExcludeAmbiguous flag.

##### Method: `Generate(PasswordOptions)`

**Decision table** (see [Section 6.2](#62-passwordgeneratorgenerate)):

| # | Test | Technique | Why |
|---|------|-----------|-----|
| 1 | Default options -> length 20, contains upper + lower + digit + special | Unit | Happy path |
| 2 | Length = 12 (min boundary) | BVA | Boundary |
| 3 | Length = 128 (max boundary) | BVA | Boundary |
| 4 | Length = 11 -> Fail | BVA | Below boundary |
| 5 | Length = 129 -> Fail | BVA | Above boundary |
| 6 | Only uppercase = true, rest false | Decision table | Single character set |
| 7 | Only lowercase = true | Decision table | Single character set |
| 8 | Only digits = true | Decision table | Single character set |
| 9 | Only special = true | Decision table | Single character set |
| 10 | All flags false -> Fail | Decision table | No character set |
| 11 | All true + ExcludeAmbiguous -> no ambiguous chars | Decision table + ECT | Ambiguous exclusion |
| 12 | Run 10 times -> all results are unique (randomness) | Unit | Statistical property |

---

#### 5.3.3 `SessionService`

**File:** `Services/Implementations/SessionService.cs`

**What to mock:** Nothing — self-contained. Uses only `System.Timers.Timer`.

**Fixture:** None (use `IDisposable` on the test class to dispose after each test).

**Why no shared fixture:** `SessionService` is **stateful** — mutable internal state (`_derivedKey`, `_currentUserId`, `_disposed`). Each test must start with a **fresh instance** to avoid coupling. If tests shared a fixture, one test's `ClearSession()` would corrupt another.

**Reasoning for strategy:** `SessionService` has a clear **state machine**:
```
[Created] --SetDerivedKey--> [Active] --ClearSession--> [Locked]
                                |                          |
                                +---Dispose--> [Disposed] <+
```
This makes **state-based testing** the primary technique, supplemented by **OOP testing** for the `IDisposable` contract.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`SetDerivedKey(byte[])`** | **State-based + ECT** | Transitions Created -> Active. ECT: (1) valid key -> Active, (2) null -> `ArgumentNullException`, (3) second call clears old key. |
| **`GetDerivedKey()`** | **State-based** | Active -> returns clone. Created/Locked -> throws `InvalidOperationException`. Disposed -> throws `ObjectDisposedException`. |
| **`SetUser(Guid, string, string?)`** | **Unit test + ECT** | ECT: (1) valid inputs, (2) null email -> `ArgumentNullException`. |
| **`GetAccessToken()`** | **State-based** | Returns token or null. After dispose -> `ObjectDisposedException`. |
| **`CurrentUserId` / `CurrentUserEmail`** | **State-based** | Null before `SetUser`, populated after, null after `ClearSession`, throws after `Dispose`. |
| **`ClearSession()`** | **State-based** | Active -> Locked. Key zeroed, user cleared, `VaultLocked` event fires. After dispose -> returns silently. |
| **`IsActive()`** | **State-based** | False in Created, true after `SetDerivedKey`, false after `ClearSession`, throws after `Dispose`. |
| **`ResetInactivityTimer()`** | **State-based** | Only callable when not disposed. |
| **`InactivityTimeout` property** | **ECT + BVA** | Positive -> success. `TimeSpan.Zero` or negative -> `ArgumentOutOfRangeException`. |
| **Inactivity timer expiry** | **State-based** | Set short timeout (~100ms), set key, wait -> verify `ClearSession` called and `VaultLocked` fired. |
| **`Dispose()`** | **OOP test (IDisposable)** | After dispose: all methods throw `ObjectDisposedException` except `ClearSession`. Double dispose does not throw. |
| **`VaultLocked` event** | **OOP test (events)** | Subscribe, trigger `ClearSession`, verify handler called. |
| **Key clone verification** | **OOP test (encapsulation)** | `GetDerivedKey` returns clone; modifying it does not affect internal state. |

---

#### 5.3.4 `VaultService`

**File:** `Services/Implementations/VaultService.cs`

**What to mock:**
- `ICryptoService` — control encryption/decryption results
- `ISessionService` — control active/inactive state, user ID, derived key
- `IVaultRepository` — control what entities are "in the database"
- `ILogger<VaultService>` — avoid null references

**Why mock all 4:** `VaultService` is an orchestrator — it coordinates crypto, session, and repository. Mocking lets us test every code path.

**Fixture: `VaultServiceFixture` (already exists)**

**Why a fixture:** Every test needs the same 4 mocks. The fixture encapsulates mock creation and provides helper methods (`SetupActiveSession`, `SetupInactiveSession`, `BuildEncryptedEntity`). Each test calls `fixture.Reset()` then configures mock behaviour specific to its scenario.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`GetAllEntriesAsync()`** | **Unit test** | (1) Session inactive -> Fail. (2) Active, repo returns entities, crypto decrypts -> entries. (3) Some entities fail decryption -> skipped. |
| **`GetEntryAsync(string)`** | **ECT + Unit test** | Input `id` partitions: (1) invalid GUID -> Fail, (2) valid GUID not found -> Fail, (3) found + decryption succeeds -> Ok, (4) decryption fails -> Fail, (5) repo throws -> Fail. |
| **`AddEntryAsync(VaultEntry)`** | **Unit test** | Paths: (1) session inactive -> Fail, (2) new entry (empty Id) -> generates new Id, (3) existing entry -> preserves Id, (4) encryption fails -> Fail, (5) repo throws -> Fail, (6) happy path -> Ok. |
| **`DeleteEntryAsync(string)`** | **ECT + Unit test** | Partitions: (1) session inactive -> Fail, (2) invalid GUID -> Fail, (3) valid + success -> Ok, (4) repo throws -> Fail. |
| **`SearchEntries(string, List<VaultEntry>)`** | **ECT** | Pure function — no mocking. Partitions: (1) null/empty query -> all, (2) matches WebsiteName, (3) matches Username, (4) matches Url, (5) matches Notes, (6) matches Category, (7) no match -> empty, (8) case-insensitive, (9) whitespace-trimmed query. |

---

#### 5.3.5 `UserProfileService`

**File:** `Services/Implementations/UserProfileService.cs`

**What to mock:**
- `IVaultRepository` — control database behaviour

**Why mock:** Thin wrapper around `IVaultRepository`. Mock to simulate success, `PostgrestException`, and generic exceptions.

**Fixture:** None — only one dependency, simple per-test setup.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`CreateProfileAsync(UserProfileEntity)`** | **Unit test** | Three paths: (1) success -> `Result.Ok()`, (2) `PostgrestException` -> `Result.Fail` with DB error, (3) generic exception -> `Result.Fail`. |
| **`GetProfileAsync(Guid)`** | **Unit test** | Four paths: (1) found -> `Result.Ok(profile)`, (2) null -> `Result.Fail("not found")`, (3) `PostgrestException` -> Fail, (4) generic exception -> Fail. |

---

#### 5.3.6 `ZxcvbnPasswordStrengthChecker`

**File:** `Services/Implementations/ZxcvbnPasswordStrengthChecker.cs`

**Excluded from coverage** via `[ExcludeFromCodeCoverage]`. This is a thin wrapper around the third-party `Zxcvbn.Core` library. Testing it would just test the library, not our code.

**No tests needed** — the attribute excludes it from coverage metrics.

---

### 5.4 Services (Integration — Local Supabase)

These services depend on `Supabase.Client` (a concrete class, not an interface). They cannot be effectively unit-tested with mocks because:
- `Supabase.Client.Auth` is a concrete type with methods like `SignUp`, `SignIn`, `SignOut`
- `Supabase.Client.From<T>()` returns Postgrest query builders that are difficult to mock
- Mocking would just test mock setup, not real database/auth behaviour

**Solution: Integration tests with local Supabase Docker stack.**

---

#### 5.4.1 `AuthService` — Integration Tests

**File:** `Services/Implementations/AuthService.cs`

**What to mock:**
- **Nothing is mocked** — use real `Supabase.Client`, real `CryptoService`, real `SessionService`, real `UserProfileService`, real `VaultRepository`, real `SupabaseExceptionMapper`
- The entire dependency chain is real, hitting the local Supabase Docker instance

**Why integration testing is the right strategy:**
1. `AuthService` orchestrates 6 dependencies including `Supabase.Client` (concrete). Mocking all of them would create fragile tests that verify mock wiring, not actual auth flows.
2. The real value is testing the full flow: SignUp -> user created in Supabase Auth -> `handle_new_user_profile` trigger creates profile -> Login -> key derivation -> verification token decrypt.
3. RLS policies, database triggers, and Supabase Auth behaviour can only be verified with a real instance.

**Fixture: `SupabaseFixture` (new — `IAsyncLifetime` + `IClassFixture`)**

**Why a fixture:**
- Creating a `Supabase.Client` is expensive (HTTP connection, initialization)
- The fixture should:
  1. Read `Supabase__Url` and `Supabase__AnonKey` from environment variables (set by `supabase status`)
  2. Create and initialize a `Supabase.Client`
  3. Create real `CryptoService`, `SessionService`, `VaultRepository`, `UserProfileService`, `SupabaseExceptionMapper`, `ILogger`
  4. Build and expose an `AuthService` (and `VaultRepository` for the repo integration tests)
  5. Provide a helper to generate unique test emails (to avoid conflicts between test runs)
  6. On dispose, clean up any test users if possible

**Why `IAsyncLifetime`:** `Supabase.Client.InitializeAsync()` is async and must complete before any test runs.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`RegisterAsync(email, password)` — validation** | **ECT** | Invalid email -> fail. Invalid password (too short, missing special chars) -> fail. These exercise the validators before any Supabase calls. Partitions: empty email, invalid format email, password too short, password missing required chars. |
| **`RegisterAsync(email, password)` — happy path** | **Integration test** | Register with valid email/password. Verify `Result.Ok()`. Then verify: (1) user exists in Supabase Auth (can login), (2) `UserProfiles` row created by trigger (salt + verification token stored). This is the most critical test — it validates the full registration flow including the DB trigger. |
| **`RegisterAsync(email, password)` — duplicate email** | **Integration test** | Register same email twice. Second call should fail with "already exists". Tests Supabase's 422 response mapped through `SupabaseExceptionMapper`. |
| **`LoginAsync(email, password)` — happy path** | **Integration test** | After successful registration, login with same credentials. Verify: `Result.Ok()`, `CurrentUserId` is set, `CurrentUserEmail` is set, `SessionService.IsActive()` is true, `SessionService.GetDerivedKey()` returns a 32-byte key. |
| **`LoginAsync(email, password)` — wrong password** | **Integration test** | Register, then login with wrong password. Should fail with auth error. |
| **`LoginAsync(email, password)` — non-existent user** | **Integration test** | Login with email that was never registered. Should fail. |
| **`LockAsync()`** | **Integration test** | Login, then lock. Verify: session cleared, `IsLocked()` returns true, `IsActive()` returns false. |
| **`IsLocked()`** | **Integration test** | Before login -> true. After login -> false. After lock -> true. |
| **`ChangeMasterPasswordAsync(current, new)`** | **Unit test** | Always returns `Result.Fail("Not implemented.")`. Trivial. |
| **`ValidateCredentials` (private, tested via public API)** | **ECT** | Tested indirectly through `RegisterAsync` with invalid inputs. |
| **`OnAuthStateChanged` (private event handler)** | **Integration test** | Tested indirectly: SignOut triggers `SignedOut` state -> `ClearSession`. TokenRefreshed tested implicitly through long-lived sessions. |

##### AuthService Test Class Structure

```csharp
public class AuthServiceIntegrationTests : IClassFixture<SupabaseFixture>, IAsyncLifetime
{
    private readonly SupabaseFixture _fixture;

    // Each test gets a unique email via fixture helper
    // Tests that need a registered user call RegisterAsync first
    // Tests clean up by calling LockAsync at the end
}
```

---

#### 5.4.2 `VaultRepository` — Integration Tests

**File:** `Services/Implementations/VaultRepository.cs`

**What to mock:** Nothing — test against real local Supabase.

**Why integration testing:**
1. `VaultRepository` is a thin data-access layer. Every method delegates to `Supabase.Client`. Mocking would just verify mock calls.
2. The real value is testing: RLS policies enforce user isolation, CRUD operations work with real Postgrest, `UserProfiles_pkey` and `VaultEntries_pkey` constraints are enforced.

**Fixture: Same `SupabaseFixture` as AuthService**

**Why shared fixture:** Both `AuthService` and `VaultRepository` tests need a Supabase client. The fixture creates one client and exposes both services.

**Test setup:** Each test first registers and logs in a user (via `AuthService`), which authenticates the Supabase client's session. Then it exercises `VaultRepository` CRUD operations.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`CreateUserProfileAsync(UserProfileEntity)`** | **Integration test** | Tested indirectly through `AuthService.RegisterAsync` (the `handle_new_user_profile` trigger creates the profile). Direct test: register user, verify profile exists via `GetUserProfileAsync`. Also test: creating a duplicate profile should throw `PostgrestException` (primary key violation). |
| **`GetUserProfileAsync(Guid)`** | **Integration test + ECT** | Partitions: (1) existing user -> returns profile with correct salt/token, (2) non-existent UUID -> returns null (caught by `InvalidOperationException` handler). Also verify RLS: user A cannot read user B's profile. |
| **`GetAllEntriesAsync(Guid)`** | **Integration test** | (1) No entries -> empty list. (2) Insert entries, retrieve -> all returned, ordered by `UpdatedAt` descending. (3) RLS: user A cannot see user B's entries. |
| **`GetEntryAsync(Guid, Guid)`** | **Integration test + ECT** | Partitions: (1) existing entry -> returns it, (2) non-existent entry -> returns null, (3) entry belongs to different user -> returns null (RLS). |
| **`UpsertEntryAsync(VaultEntryEntity)`** | **Integration test** | (1) Insert new entry -> appears in GetAll. (2) Update existing entry -> `UpdatedAt` changes, data updated. (3) Verify `UpdatedAt` is set to `DateTime.UtcNow` by the method. |
| **`DeleteEntryAsync(Guid, Guid)`** | **Integration test** | (1) Delete existing entry -> no longer in GetAll. (2) Delete non-existent entry -> no error (idempotent). (3) RLS: user A cannot delete user B's entry. |

##### RLS Policy Tests (Cross-Cutting)

These are critical security tests unique to the integration approach:

| # | Test | Expected | Why |
|---|------|----------|-----|
| 1 | User A reads User B's profile | Not found (RLS blocks) | `profiles_select_own` policy |
| 2 | User A reads User B's vault entries | Empty list (RLS filters) | `Users can read own vault entries` policy |
| 3 | User A deletes User B's vault entry | No effect (RLS blocks) | `Users can delete own vault entries` policy |
| 4 | User A inserts entry with User B's UserId | Should fail (RLS `WITH CHECK`) | `Users can insert own vault entries` policy |

---

### 5.5 Extensions

#### 5.5.1 `VaultEntryExtensions`

**File:** `Extensions/VaultEntryExtensions.cs`

**What to mock:** Nothing — pure extension methods with no dependencies.

**Fixture:** None.

**Note:** `VaultEntryPayload` now uses `required init` properties (not a positional constructor). The extension `ToPayload` constructs with object initializer syntax.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`ToPayload(VaultEntry)`** | **Unit test + ECT** | Verify all 7 fields map correctly. ECT: populated fields (valid class), null fields (default to `""`). |
| **`ToVaultEntry(VaultEntryPayload, Guid, DateTime, DateTime)`** | **Unit test + ECT** | All fields map + metadata. ECT: null payload fields -> `""`. |
| **`ToVaultEntry(VaultEntryPayload, VaultEntryEntity)`** | **Unit test** | Verify delegation to 3-parameter overload. |
| **Round-trip: `ToPayload` -> `ToVaultEntry`** | **Slice test** | Create VaultEntry, convert to payload, convert back. All fields survive. |

---

### 5.6 Exception Mapper

#### 5.6.1 `SupabaseExceptionMapper`

**File:** `Exceptions/SupabaseExceptionMapper.cs`

**What to mock:** Nothing — pure function.

**Fixture:** None.

**Why decision table:** The mapper has a clear decision structure: pattern-match on exception type, then for `GotrueException` match on status code, then fall back to message content matching.

##### Method: `MapAuthException(Exception)`

**Decision table** (see [Section 6.3](#63-supabaseexceptionmapper)):

| # | Exception Type | Status Code | Message Content | Expected Result |
|---|---------------|-------------|-----------------|-----------------|
| 1 | `GotrueException` | 422 | (any) | "already exists" |
| 2 | `GotrueException` | 400 | (any) | "Invalid request" |
| 3 | `GotrueException` | 429 | (any) | "Too many attempts" |
| 4 | `GotrueException` | other | "already registered" | "already exists" |
| 5 | `GotrueException` | other | "user_already_exists" | "already exists" |
| 6 | `GotrueException` | other | "invalid" | "Invalid email or password" |
| 7 | `GotrueException` | other | other message | "Authentication failed" |
| 8 | `HttpRequestException` | - | - | "Network error" |
| 9 | Other exception | - | - | "unexpected error" |

---

## 6. Decision Tables

### 6.1 PasswordOptionsValidator

| Rule | Length in [12, 128]? | >= 1 char type selected? | Expected |
|------|---------------------|------------------------|----------|
| R1 | Yes | Yes | Valid |
| R2 | Yes | No (all false) | Invalid |
| R3 | No (< 12) | Yes | Invalid |
| R4 | No (> 128) | Yes | Invalid |
| R5 | No | No | Invalid - both errors |

### 6.2 PasswordGenerator.Generate

Flags: `IncludeUppercase (U)`, `IncludeLowercase (L)`, `IncludeDigits (D)`, `IncludeSpecialCharacters (S)`, `ExcludeAmbiguousCharacters (A)`

| Rule | U | L | D | S | A | Expected |
|------|---|---|---|---|---|----------|
| R1 | T | T | T | T | F | All four char types present |
| R2 | T | T | T | T | T | All four types, no ambiguous chars |
| R3 | T | F | F | F | F | Uppercase only |
| R4 | F | T | F | F | F | Lowercase only |
| R5 | F | F | T | F | F | Digits only |
| R6 | F | F | F | T | F | Special chars only |
| R7 | F | F | F | F | F | Fail - no char types (caught by validator) |
| R8 | T | T | F | F | F | Upper + lower only |
| R9 | T | F | T | F | T | Upper + digits, no ambiguous |
| R10 | F | T | F | T | T | Lower + special, no ambiguous |

### 6.3 SupabaseExceptionMapper

| Rule | Exception Type | Status Code | Message Contains | Result |
|------|---------------|-------------|-----------------|--------|
| R1 | GotrueException | 422 | - | "already exists" |
| R2 | GotrueException | 400 | - | "Invalid request" |
| R3 | GotrueException | 429 | - | "Too many attempts" |
| R4 | GotrueException | other | "already registered" | "already exists" |
| R5 | GotrueException | other | "user_already_exists" | "already exists" |
| R6 | GotrueException | other | "invalid" | "Invalid email or password" |
| R7 | GotrueException | other | other | "Authentication failed" |
| R8 | HttpRequestException | - | - | "Network error" |
| R9 | (other) | - | - | "unexpected error" |

---

## 7. Equivalence Class Partitions

### 7.1 Password Validation

| Partition | Representative | Expected |
|-----------|---------------|----------|
| **Valid** (12-128 chars, all types) | `"Abcdefgh1!xy"` | Valid |
| **Empty** | `""` | Invalid |
| **Too short** (1-11 chars) | `"Ab1!"` | Invalid |
| **Too long** (> 128 chars) | 129-char string | Invalid |
| **Missing uppercase** (12+ chars) | `"abcdefgh1!xy"` | Invalid |
| **Missing lowercase** (12+ chars) | `"ABCDEFGH1!XY"` | Invalid |
| **Missing digit** (12+ chars) | `"Abcdefghij!x"` | Invalid |
| **Missing special** (12+ chars) | `"Abcdefghij1x"` | Invalid |

### 7.2 Email Validation

| Partition | Representative | Expected |
|-----------|---------------|----------|
| **Valid** | `"user@example.com"` | Valid |
| **Empty** | `""` | Invalid |
| **No @** | `"userexample.com"` | Invalid |
| **No domain** | `"user@"` | Invalid |
| **Uppercase local part** | `"User@example.com"` | Invalid (regex) |
| **At max (256 chars)** | 256-char email | Valid |
| **Over max (257 chars)** | 257-char email | Invalid |

### 7.3 Encryption Key

| Partition | Representative | Expected |
|-----------|---------------|----------|
| **Valid (32 bytes)** | `new byte[32]` | Valid |
| **Null** | `null` | Invalid |
| **Too short (31 bytes)** | `new byte[31]` | Invalid |
| **Too long (33 bytes)** | `new byte[33]` | Invalid |
| **Empty (0 bytes)** | `new byte[0]` | Invalid |

### 7.4 Base64 Encrypted Blob

| Partition | Representative | Expected |
|-----------|---------------|----------|
| **Valid (28+ bytes decoded)** | base64 of 28+ byte array | Valid |
| **Empty** | `""` | Invalid |
| **Null** | `null` | Invalid |
| **Non-base64** | `"%%%"` | Invalid |
| **Valid base64, too short** | base64 of 27-byte array | Invalid |

---

## 8. OOP Testing Considerations

### 8.1 Inheritance Testing

- **`Result<T>` extends `Result`**: Verify polymorphic assignment.
- **`VaultEntryEntity` / `UserProfileEntity` extend `BaseModel`**: Verify they can be used where `BaseModel` is expected.

### 8.2 Interface Contract Testing

| Interface | Implementation | Key Contract |
|-----------|---------------|-------------|
| `ICryptoService` | `CryptoService` | Encrypt -> Decrypt round-trip recovers plaintext |
| `IPasswordGenerator` | `PasswordGenerator` | Generated passwords meet all enabled constraints |
| `ISessionService` | `SessionService` | Set -> Get -> Clear lifecycle; `IDisposable` contract |
| `ISupabaseExceptionMapper` | `SupabaseExceptionMapper` | All exception types produce user-friendly messages |

### 8.3 `IDisposable` Testing (`SessionService`)

| # | Test | Expected |
|---|------|----------|
| 1 | After `Dispose()`, `GetDerivedKey()` throws `ObjectDisposedException` | Exception |
| 2 | After `Dispose()`, `SetDerivedKey(key)` throws `ObjectDisposedException` | Exception |
| 3 | After `Dispose()`, `SetUser(...)` throws `ObjectDisposedException` | Exception |
| 4 | After `Dispose()`, `IsActive()` throws `ObjectDisposedException` | Exception |
| 5 | After `Dispose()`, `ResetInactivityTimer()` throws `ObjectDisposedException` | Exception |
| 6 | After `Dispose()`, `ClearSession()` does NOT throw (graceful) | No exception |
| 7 | Double `Dispose()` does NOT throw | No exception |
| 8 | `InactivityTimeout` getter/setter after `Dispose()` throws `ObjectDisposedException` | Exception |

### 8.4 Encapsulation Testing

- **`SessionService.GetDerivedKey()` returns a clone**: Modify returned array -> call again -> internal state unchanged.

### 8.5 Event Testing

- **`SessionService.VaultLocked` event**: Subscribe, call `ClearSession()`, verify event handler invoked.

---

## 9. Additional Recommended Techniques

### 9.1 Boundary Value Analysis (BVA)

| Parameter | Min | Min-1 | Max | Max+1 |
|-----------|-----|-------|-----|-------|
| Password length | 12 | 11 | 128 | 129 |
| Encryption key size | 32 | 31 | 32 | 33 |
| Email length | 1 | 0 | 256 | 257 |
| Nonce size | 12 | 11 | 12 | 13 |
| Tag size | 16 | 15 | 16 | 17 |
| Generated password length | 12 | 11 | 128 | 129 |

### 9.2 State Transition Testing (`SessionService`)

```
[Created] --SetDerivedKey()--> [Active] --ClearSession()--> [Locked]
                                  |                            |
                                  +---Dispose()--> [Disposed] <+
```

Test each valid transition. Test that invalid operations in each state produce correct errors.

### 9.3 Slice Testing Summary

| Slice | Components Involved | What It Verifies |
|-------|-------------------|-----------------|
| Encrypt -> Decrypt | `CryptoService.Encrypt`, `CryptoService.Decrypt` | Round-trip data integrity |
| Blob serialisation | `EncryptedBlob.ToBase64String`, `EncryptedBlob.FromBase64String` | Serialisation/deserialisation consistency |
| Payload serialisation | `VaultEntryPayload.ToJson`, `VaultEntryPayload.FromJson` | JSON round-trip |
| Entry mapping | `VaultEntry` -> `ToPayload` -> `ToVaultEntry` | Field mapping consistency |
| Full vault flow | `VaultEntry` -> `ToPayload` -> `ToJson` -> `Encrypt` -> `ToBase64String` -> `FromBase64String` -> `Decrypt` -> `FromJson` -> `ToVaultEntry` | Complete data pipeline |

### 9.4 Integration Testing Slices (Local Supabase)

| Slice | Components | What It Verifies |
|-------|-----------|-----------------|
| Registration flow | `AuthService.RegisterAsync` -> Supabase Auth -> `handle_new_user_profile` trigger -> `UserProfiles` table | Full signup pipeline including DB trigger |
| Login flow | `AuthService.LoginAsync` -> Supabase Auth -> `UserProfileService.GetProfileAsync` -> key derivation -> token verification | Full authentication pipeline |
| Vault CRUD | `VaultRepository` -> Supabase Postgrest -> `VaultEntries` table -> RLS policies | Data persistence with security |
| Cross-user isolation | User A vs User B via RLS | Row-Level Security enforcement |

### 9.5 Pairwise / Combinatorial Testing

`PasswordOptions` has 5 boolean flags + 1 integer. Full combination = 2^5 x many lengths = huge. Use pairwise testing:

| U | L | D | S | A |
|---|---|---|---|---|
| T | T | T | T | F |
| T | F | F | F | T |
| F | T | F | T | F |
| F | F | T | F | T |
| T | T | F | F | F |
| F | F | F | T | T |

### 9.6 Error Guessing

| Area | Error Guess |
|------|------------|
| `CryptoService.DeriveKey` | Empty password, Unicode password (emoji), very long password |
| `CryptoService.Encrypt` | Unicode plaintext (emoji, CJK characters) |
| `EncryptedBlob.ToBase64String` | Blob with zero-length ciphertext |
| `VaultService.SearchEntries` | Entries with null properties, empty list |
| `SessionService` | Concurrent `SetDerivedKey` / `ClearSession` from different threads |
| `AuthService.RegisterAsync` | Supabase down (connection refused) |
| `VaultRepository` | Inserting entry with non-existent UserId (FK violation) |

---

## 10. Coverage Goals and Measurement

### 10.1 Running Coverage

```bash
# Run tests with coverage
dotnet test PasswordManager.Tests/PasswordManager.Tests.csproj \
  --collect:"XPlat Code Coverage"

# Or with coverlet.msbuild
dotnet test PasswordManager.Tests/PasswordManager.Tests.csproj \
  /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Generate HTML report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html
```

### 10.2 Targets

| Metric | Target | Notes |
|--------|--------|-------|
| **Line coverage** | >= 80% | All Core classes |
| **Branch coverage** | >= 75% | Important for validators and decision logic |
| **Method coverage** | 100% | Every public/internal method has >= 1 test |
| **Class coverage** | 100% | Every class has a test class |

### 10.3 Exclusions

Exclude from coverage metrics:
- `ZxcvbnPasswordStrengthChecker` — marked `[ExcludeFromCodeCoverage]` (third-party wrapper)
- Private helper methods in `PasswordValidator` — marked `[ExcludeFromCodeCoverage]` (simple LINQ one-liners tested via public API)

### 10.4 CI Enforcement (Jenkins)

```bash
# Supabase start + reset for integration tests
supabase start
supabase db reset
eval "$(supabase status --output env)"
export Supabase__Url="$SUPABASE_URL"
export Supabase__AnonKey="$SUPABASE_ANON_KEY"
export Supabase__ServiceRoleKey="$SUPABASE_SERVICE_ROLE_KEY"

dotnet test $TESTS_PROJECT_PATH /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

---

## 11. Test Organisation and Naming Conventions

### 11.1 Directory Structure

```
PasswordManager.Tests/
├── Data/
│   └── TestData.cs                          (existing)
├── Fixtures/
│   ├── CryptoServiceFixture.cs              (existing)
│   ├── EncryptedBlobFixture.cs              (existing)
│   ├── VaultServiceFixture.cs               (existing)
│   └── SupabaseFixture.cs                   (NEW — for AuthService + VaultRepository integration)
├── Models/
│   ├── ResultTests.cs                       (existing)
│   ├── EncryptedBlobTests.cs                (existing)
│   ├── PasswordOptionsTests.cs              (existing)
│   ├── VaultEntryTests.cs                   (existing)
│   ├── VaultEntryPayloadTests.cs            (existing)
│   └── PasswordPolicyTests.cs
├── Validation/
│   ├── PasswordValidatorTests.cs            (existing)
│   ├── EmailValidatorTests.cs               (existing)
│   ├── EncryptionInputValidatorTests.cs     (existing)
│   ├── DecryptionInputValidatorTests.cs     (existing)
│   ├── PasswordOptionsValidatorTests.cs     (existing)
│   └── EncryptedBlobFormatValidatorTests.cs (existing)
├── Services/
│   ├── CryptoServiceTests.cs               (existing)
│   ├── PasswordGeneratorTests.cs            (NEW)
│   ├── SessionServiceTests.cs              (existing)
│   ├── VaultServiceTests.cs                (existing)
│   └── UserProfileServiceTests.cs          (existing)
├── Integration/
│   ├── AuthServiceIntegrationTests.cs       (NEW)
│   └── VaultRepositoryIntegrationTests.cs   (NEW)
├── Extensions/
│   └── VaultEntryExtensionsTests.cs
└── Exceptions/
    └── SupabaseExceptionMapperTests.cs
```

### 11.2 Naming Convention

**Pattern:** `MethodName_Scenario_ExpectedBehavior`

Examples:
- `Encrypt_ValidPlaintextAndKey_ReturnsSuccessResult`
- `Encrypt_EmptyPlaintext_ReturnsFailResult`
- `DeriveKey_SamePasswordAndSalt_ReturnsSameKey`
- `Validate_PasswordTooShort_ReturnsInvalid`
- `SearchEntries_EmptyQuery_ReturnsAllEntries`
- `ClearSession_WhenActive_FiresVaultLockedEvent`
- `GetDerivedKey_AfterDispose_ThrowsObjectDisposedException`
- `RegisterAsync_ValidCredentials_CreatesUserAndProfile` (integration)
- `GetAllEntriesAsync_UserBCannotSeeUserAEntries_ReturnsEmptyList` (integration + RLS)

### 11.3 Test Attributes

| Attribute | Use For |
|-----------|---------|
| `[Fact]` | Single deterministic test case |
| `[Theory]` + `[InlineData(...)]` | Parameterised tests (equivalence classes, decision tables) |
| `[Trait("Category", "Unit")]` | Unit tests |
| `[Trait("Category", "Integration")]` | Integration tests (require Supabase) |
| `[Trait("Category", "BoundaryValue")]` | Boundary tests |
| `[Trait("Category", "DecisionTable")]` | Decision table tests |
| `[Trait("Category", "SliceTest")]` | Slice tests |

### 11.4 Running Test Categories Separately

```bash
# Unit tests only (no Supabase required)
dotnet test --filter "Category=Unit"

# Integration tests only (requires running Supabase)
dotnet test --filter "Category=Integration"

# All tests
dotnet test
```

### 11.5 AAA Pattern

Every test follows Arrange-Act-Assert:

```csharp
// Arrange — create inputs, configure mocks (or set up Supabase state)
// Act — call the method under test
// Assert — verify the result
```

---

## Summary: Fixtures, Mocks, and Strategy by Component

| Component | Fixture? | Mocks? | Primary Strategy | Why |
|-----------|----------|--------|-----------------|-----|
| `Result` / `Result<T>` | No | No | Unit test + OOP | Simple factories; test polymorphism |
| `EncryptedBlob` | **Yes** (`EncryptedBlobFixture`) | No | ECT + BVA + Slice | Pre-built test scenarios; round-trip slice |
| `PasswordOptions` | No | No | Unit test | Pure POCO defaults |
| `VaultEntry` | No | No | Unit test + OOP | POCO + init-only verification |
| `VaultEntryPayload` | No | No | ECT + Slice | JSON parse partitions; round-trip slice |
| `PasswordPolicy` | No | No | Unit test | Constants verification |
| All 6 Validators | No | No | **ECT + BVA** | Validators are classifiers |
| `PasswordOptionsValidator` | No | No | **Decision table** | Boolean conditions form truth table |
| `CryptoService` | **Yes** (`CryptoServiceFixture`) | No | Unit + Slice | Expensive Argon2; encrypt/decrypt round-trip |
| `PasswordGenerator` | No | No | **Decision table + BVA** | Boolean flag combos; length boundaries |
| `SessionService` | No (fresh per test) | No | **State-based + OOP** | Stateful lifecycle; IDisposable |
| `VaultService` | **Yes** (`VaultServiceFixture`) | **Yes** (4 mocks) | Unit test + ECT | Orchestrator — mock deps |
| `UserProfileService` | No | **Yes** (1 mock) | Unit test | Thin wrapper — mock repo |
| `ZxcvbnPasswordStrengthChecker` | - | - | **Excluded** | `[ExcludeFromCodeCoverage]` |
| `SupabaseExceptionMapper` | No | No | **Decision table** | Status code x message -> output |
| `VaultEntryExtensions` | No | No | Unit + ECT + Slice | Pure mappers; null handling; round-trip |
| **`AuthService`** | **Yes** (`SupabaseFixture`) | **No** (real Supabase) | **Integration test** | Concrete Supabase dependency; full auth flow with DB triggers and RLS |
| **`VaultRepository`** | **Yes** (`SupabaseFixture`) | **No** (real Supabase) | **Integration test** | Thin data-access layer; real Postgrest + RLS policies |

**Total estimated test cases: ~200+** (including integration tests)
