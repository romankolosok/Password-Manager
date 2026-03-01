# Password Manager – Core Library Testing Plan (Updated)

## Table of Contents

1. [Overview & Scope](#1-overview--scope)
2. [Testing Infrastructure & Packages](#2-testing-infrastructure--packages)
3. [Accessibility Changes Required](#3-accessibility-changes-required)
4. [Testing Techniques Glossary & When to Use Each](#4-testing-techniques-glossary--when-to-use-each)
5. [Detailed Test Plan by Component](#5-detailed-test-plan-by-component)
   - 5.1 [Models](#51-models)
   - 5.2 [Validators](#52-validators)
   - 5.3 [Services](#53-services)
   - 5.4 [Extensions](#54-extensions)
   - 5.5 [Exception Mapper](#55-exception-mapper)
6. [Decision Tables](#6-decision-tables)
7. [Equivalence Class Partitions](#7-equivalence-class-partitions)
8. [OOP Testing Considerations](#8-oop-testing-considerations)
9. [Additional Recommended Techniques](#9-additional-recommended-techniques)
10. [Coverage Goals & Measurement](#10-coverage-goals--measurement)
11. [Test Organisation & Naming Conventions](#11-test-organisation--naming-conventions)

---

## 1. Overview & Scope

This document provides a **detailed, per-component and per-method** testing strategy for the **PasswordManager.Core** library. For every class and method, we explain:

- **What to mock** and why
- **What fixture to create** (shared setup via `IClassFixture<T>` or inline)
- **Which testing technique** applies (unit test, equivalence class testing, decision table testing, boundary value analysis, state-based testing, slice testing) and **why it is the most appropriate** for that specific method

### Core Library Inventory

| Layer | Classes | Count |
|-------|---------|-------|
| **Models** | `Result`, `Result<T>`, `EncryptedBlob`, `PasswordOptions`, `VaultEntry`, `VaultEntryPayload`, `PasswordPolicy` | 7 |
| **Entities** | `UserProfileEntity`, `VaultEntryEntity` | 2 |
| **Validators** | `PasswordValidator`, `EmailValidator`, `EncryptionInputValidator`, `DecryptionInputValidator`, `PasswordOptionsValidator`, `EncryptedBlobFormatValidator` | 6 |
| **Services** | `CryptoService`, `SessionService`, `VaultService`, `UserProfileService`, `AuthService`, `VaultRepository`, `ZxcvbnPasswordStrengthChecker` | 7 |
| **Interfaces** | `ICryptoService`, `ISessionService`, `IVaultService`, `IAuthService`, `IVaultRepository`, `IUserProfileService`, `IPasswordStrengthChecker`, `IClipboardService` | 8 |
| **Extensions** | `VaultEntryExtensions` | 1 |
| **Exceptions** | `SupabaseExceptionMapper` | 1 |

---

## 2. Testing Infrastructure & Packages

### Already in `PasswordManager.Tests.csproj`

| Package | Version | Purpose |
|---------|---------|---------|
| `xunit` | 2.5.3 | Test framework (`[Fact]`, `[Theory]`) |
| `xunit.runner.visualstudio` | 2.5.3 | IDE / `dotnet test` integration |
| `Microsoft.NET.Test.Sdk` | 17.9.0 | Test host |
| `Moq` | 4.20.72 | Mocking interfaces for isolation |
| `FluentAssertions` | 8.8.0 | Expressive assertions |
| `coverlet.collector` | 6.0.0 | Code coverage collection (XPlat Code Coverage) |
| `JunitXml.TestLogger` | 8.0.0 | JUnit XML output for Jenkins |

### Recommended Additions

| Package | Purpose | Why |
|---------|---------|-----|
| `coverlet.msbuild` | MSBuild-integrated coverage with thresholds (`/p:Threshold=80`) | Can fail builds when coverage drops below target |
| `ReportGenerator` (global tool) | Converts Cobertura XML → HTML reports | `dotnet tool install -g dotnet-reportgenerator-globaltool` |

No other packages are needed. The existing stack (xUnit + Moq + FluentAssertions + Coverlet) is industry-standard for .NET unit testing.

---

## 3. Accessibility Changes Required

Several classes are `internal`. To test them directly, the Core project already has:

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

## 4. Testing Techniques Glossary & When to Use Each

| Technique | Description | When to Use |
|-----------|-------------|-------------|
| **Unit Testing** | Test a single method in isolation; mock all external dependencies | Every method in the library — the foundation of the strategy |
| **Equivalence Class Testing (ECT)** | Partition inputs into classes of values that should be treated identically, then test one representative from each class | Validators (password, email, key sizes), inputs with clear valid/invalid domains |
| **Boundary Value Analysis (BVA)** | Test at the exact edges of equivalence classes (min, min-1, max, max+1) | Numeric constraints (password length 12/128, key size 32, email length 256) |
| **Decision Table Testing** | Enumerate all combinations of boolean conditions and their expected outcomes | `PasswordOptions` (5 boolean flags), `SupabaseExceptionMapper` (exception type × status code), `GeneratePassword` (flag combinations) |
| **State-Based Testing** | Test object lifecycle through state transitions (Created → Active → Locked → Disposed) | `SessionService` — the only stateful service with clear lifecycle states |
| **Slice Testing** | Test a vertical slice through multiple layers without mocking to verify integration | `CryptoService.Encrypt` → `EncryptedBlob.ToBase64String` → `FromBase64String` → `CryptoService.Decrypt` round-trip; `VaultEntry` → `ToPayload` → `ToJson` → `FromJson` → `ToVaultEntry` |
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
| `Result<T>.Fail(string)` | **Unit test** | Verify `Success = false`, `Value = default(T)`, `Message` set correctly. Test with value types (int → 0) and reference types (string → null) to cover both default behaviours. |
| Polymorphism: `Result<T>` is-a `Result` | **OOP test** | Assign `Result<int>` to a `Result` variable and verify `Success`/`Message` are accessible. This tests that the inheritance is working correctly at runtime. |

---

#### 5.1.2 `EncryptedBlob`

**File:** `Models/EncryptedBlob.cs`

**What to mock:** Nothing — `EncryptedBlob` is a model class. Internally it creates an `EncryptedBlobFormatValidator`, but that is part of the unit under test (the validator is a collaborator, not an external dependency, and we want to test the integration).

**Fixture:** None needed. Each test constructs its own blob.

**Why no fixture:** Object construction is trivial (just byte arrays). No database, no network, no slow setup.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| `ToBase64String()` | **Unit test** | Verify that concatenation of Nonce + Ciphertext + Tag is correctly base64-encoded. Deterministic output for deterministic input. |
| `FromBase64String(string)` | **Equivalence class + Boundary value** | The input string falls into distinct classes: (1) null, (2) empty, (3) non-base64 garbage, (4) valid base64 but too short (< 28 bytes decoded), (5) valid base64 exactly 28 bytes (boundary — nonce + tag, zero-length ciphertext), (6) valid base64 with ciphertext. Each class has different expected behaviour. Boundary at 27 vs 28 bytes is critical. |
| Round-trip: `ToBase64String` → `FromBase64String` | **Slice test** | This is a vertical slice through two methods. Construct a blob, serialise, deserialise, verify all three arrays match. This catches encoding/decoding inconsistencies that per-method tests might miss. |

---

#### 5.1.3 `PasswordOptions`

**File:** `Models/PasswordOptions.cs`

**What to mock:** Nothing — pure data class (POCO).

**Fixture:** None.

**Why no fixture:** Trivial construction, no shared state.

##### Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| Default constructor | **Unit test** | Verify defaults: `Length = 20`, `IncludeUppercase = true`, `IncludeLowercase = true`, `IncludeDigits = true`, `IncludeSpecialCharacters = true`, `ExcludeAmbiguousCharacters = false`. This is a single deterministic check. |
| Property setters | **Unit test** | Verify each property can be set and read back. Trivial but ensures the POCO works. |

---

#### 5.1.4 `VaultEntry`

**File:** `Models/VaultEntry.cs`

**What to mock:** Nothing — pure data class.

**Fixture:** None.

##### Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| Default constructor | **Unit test** | Verify `Id` is a non-empty Guid, strings default to `string.Empty`, `IsFavorite = false`. |
| `Id` is init-only | **OOP test** | Verify immutability — attempting to set `Id` after construction should be prevented by the `init` accessor (compile-time, but can verify at design level). |

---

#### 5.1.5 `VaultEntryPayload` (internal record)

**File:** `Models/VaultEntryPayload.cs`

**What to mock:** Nothing — pure record type with JSON serialisation.

**Fixture:** None.

**Why this needs testing:** It is the data contract for encrypted storage. Incorrect serialisation means data loss.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| `ToJson()` | **Unit test** | Create a known payload, serialise, verify JSON contains expected fields. Deterministic. |
| `FromJson(string)` | **Equivalence class** | Input classes: (1) valid JSON matching the record shape, (2) `null`, (3) empty string `""`, (4) invalid JSON `"{ broken"`, (5) valid JSON but wrong shape (missing fields). Each class triggers a different code path (success vs `catch → null`). |
| Round-trip: `ToJson()` → `FromJson()` | **Slice test** | Serialise then deserialise. Verify all 7 fields survive the round-trip. This catches JSON property naming mismatches that per-method tests would miss. |

---

#### 5.1.6 `PasswordPolicy` (internal static class)

**File:** `Models/PasswordPolicy.cs`

**What to mock:** Nothing — constants only.

**Fixture:** None.

##### Strategy

| Test | Technique | Reasoning |
|------|-----------|-----------|
| Constants match expected values | **Unit test** | Verify `MinLength = 12`, `MaxLength = 128`, `SpecialCharacters` is the expected string. These are the contract other validators depend on. If someone changes them, tests should catch it. |

---

### 5.2 Validators

All validators use FluentValidation's `AbstractValidator<T>`. They are pure logic with no external dependencies.

**General approach for all validators:**
- **What to mock:** Nothing — validators are pure functions (input → validation result).
- **Fixture:** None — validators are cheap to construct (`new PasswordValidator()`).
- **Primary technique:** Equivalence class testing + boundary value analysis, because validators are classifiers — they partition the input domain into "valid" and "invalid" regions, making ECT the natural fit.
- **Why ECT is the best fit for validators:** A validator's job is literally to classify inputs. Equivalence classes map directly onto the validation rules. Each rule defines a partition boundary.

---

#### 5.2.1 `PasswordValidator`

**File:** `Validation/PasswordValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning for strategy choice:** The password validator has 7 rules (not-empty, min length, max length, has uppercase, has lowercase, has digit, has special char). Some rules have `.When()` conditions (character type rules only fire when length ≥ MinLength). This creates a rich input domain best covered by **equivalence class testing** for the main partitions and **boundary value analysis** for the length constraints.

##### Method: `Validate(PasswordInput)`

**Equivalence classes:**

| # | Class | Representative | Expected | Why this class |
|---|-------|---------------|----------|----------------|
| 1 | Empty / null | `""` | Invalid – "cannot be empty" | First rule fires, short-circuits other checks |
| 2 | Too short (1–11 chars), all types present | `"Ab1!cdefgh"` (10 chars) | Invalid – min length | `.When()` prevents character-type rules from firing, so only length error |
| 3 | At minimum length (12), all types present | `"Abcdefgh1!xy"` | Valid | Exact boundary of the min-length rule |
| 4 | Missing uppercase (12+ chars) | `"abcdefgh1!xy"` | Invalid – "must contain uppercase" | Character-type rule fires because length condition met |
| 5 | Missing lowercase (12+ chars) | `"ABCDEFGH1!XY"` | Invalid – "must contain lowercase" | Same reasoning |
| 6 | Missing digit (12+ chars) | `"Abcdefghij!x"` | Invalid – "must contain digit" | Same reasoning |
| 7 | Missing special char (12+ chars) | `"Abcdefghij1x"` | Invalid – "must contain special" | Same reasoning |
| 8 | At max length (128), all types present | 128-char valid string | Valid | Exact boundary of max-length rule |
| 9 | Over max length (129) | 129-char string | Invalid – "must not exceed 128" | Above max-length boundary |

**Boundary values for length:** `0`, `1`, `11`, `12`, `128`, `129`

---

#### 5.2.2 `EmailValidator`

**File:** `Validation/EmailValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** Like `PasswordValidator`, this is a classifier with 3 rules: not-empty, max-length, email format. ECT + BVA covers it well.

##### Method: `Validate(EmailInput)`

**Equivalence classes:**

| # | Class | Representative | Expected | Why this class |
|---|-------|---------------|----------|----------------|
| 1 | Empty / null | `""` | Invalid – "cannot be empty" | First rule |
| 2 | Valid standard email | `"user@example.com"` | Valid | Happy path |
| 3 | Valid minimal email | `"a@b.co"` | Valid | Shortest plausible valid email |
| 4 | Missing `@` | `"userexample.com"` | Invalid – format | Format rule |
| 5 | Missing domain | `"user@"` | Invalid – format | Format rule |
| 6 | At max length (256 chars) | 256-char valid email | Valid | Max boundary |
| 7 | Over max length (257 chars) | 257-char email | Invalid – exceeds 256 | Above boundary |

**Boundary values for length:** `0`, `1`, `256`, `257`

---

#### 5.2.3 `EncryptionInputValidator`

**File:** `Validation/EncryptionInputValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** Two independent rules (plaintext not-empty, key exactly 32 bytes) create a simple 2-condition decision table, but since each condition is independent, ECT per-condition is sufficient without a full decision table.

##### Method: `Validate(EncryptionInput)`

**Equivalence classes:**

| # | Class | Representative | Expected | Why |
|---|-------|---------------|----------|-----|
| 1 | Valid: non-empty plaintext + 32-byte key | `("hello", new byte[32])` | Valid | Happy path |
| 2 | Empty plaintext | `("", new byte[32])` | Invalid – "Plaintext cannot be null or empty" | Empty string partition |
| 3 | Null key | `("hello", null)` | Invalid – "key cannot be null" | Null partition |
| 4 | Key too short (31 bytes) | `("hello", new byte[31])` | Invalid – "key must be 32 bytes" | Boundary -1 |
| 5 | Key too long (33 bytes) | `("hello", new byte[33])` | Invalid – "key must be 32 bytes" | Boundary +1 |
| 6 | Key empty (0 bytes) | `("hello", new byte[0])` | Invalid – "key must be 32 bytes" | Edge |

---

#### 5.2.4 `DecryptionInputValidator`

**File:** `Validation/DecryptionInputValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** More conditions (blob not null, nonce 12 bytes, ciphertext not empty, tag 16 bytes, key 32 bytes), but they are still independent conditions. ECT per-condition covers the domain well. BVA at the exact byte sizes catches off-by-one errors.

##### Method: `Validate(DecryptionInput)`

**Equivalence classes:**

| # | Class | Expected | Why |
|---|-------|----------|-----|
| 1 | All valid (12-byte nonce, non-empty ciphertext, 16-byte tag, 32-byte key) | Valid | Happy path |
| 2 | Null blob | Invalid – "blob cannot be null" | Null partition |
| 3 | Nonce = 11 bytes | Invalid – "Nonce must be 12 bytes" | BVA: min - 1 |
| 4 | Nonce = 13 bytes | Invalid – "Nonce must be 12 bytes" | BVA: min + 1 |
| 5 | Empty ciphertext | Invalid – "Ciphertext cannot be empty" | Empty partition |
| 6 | Tag = 15 bytes | Invalid – "Tag must be 16 bytes" | BVA: min - 1 |
| 7 | Tag = 17 bytes | Invalid – "Tag must be 16 bytes" | BVA: min + 1 |
| 8 | Key = 31 bytes | Invalid – "key must be 32 bytes" | BVA: min - 1 |
| 9 | Key = 33 bytes | Invalid – "key must be 32 bytes" | BVA: min + 1 |

---

#### 5.2.5 `PasswordOptionsValidator`

**File:** `Validation/PasswordOptionsValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** This validator has 2 rules: (1) `Length` in `[12, 128]` and (2) at least one character set selected. The character set check is a boolean OR of 4 flags. This makes it a natural fit for a **decision table** — the combination of Length validity × character-set selection creates a small truth table.

##### Method: `Validate(PasswordOptions)`

**Decision table** (see [Section 6.1](#61-passwordoptionsvalidator)):

| Rule | Length valid? | ≥ 1 char type? | Expected |
|------|-------------|---------------|----------|
| R1 | Yes | Yes | Valid |
| R2 | Yes | No (all false) | Invalid – "at least one character set" |
| R3 | No (< 12) | Yes | Invalid – "length must be between" |
| R4 | No (> 128) | Yes | Invalid – "length must be between" |
| R5 | No | No | Invalid – both errors |

**Length boundary values:** `11` (below min), `12` (at min), `128` (at max), `129` (above max), `0`, `-1`

---

#### 5.2.6 `EncryptedBlobFormatValidator`

**File:** `Validation/EncryptedBlobFormatValidator.cs`

**What to mock:** Nothing.
**Fixture:** None.

**Reasoning:** Single validation with a helper method. The input is a base64 string that decodes to a blob. Natural fit for ECT because there are distinct input categories.

##### Method: `Validate(EncryptedBlobParseInput)`

**Equivalence classes:**

| # | Class | Representative | Expected | Why |
|---|-------|---------------|----------|-----|
| 1 | Empty string | `""` | Invalid – "cannot be empty" | Empty partition |
| 2 | Null | `null` | Invalid – "cannot be empty" | Null partition |
| 3 | Non-base64 garbage | `"%%%not-base64"` | Invalid – "Invalid format" | Format partition |
| 4 | Valid base64, < 28 bytes decoded | base64 of 27-byte array | Invalid – "at least nonce + tag" | Boundary - 1 |
| 5 | Valid base64, = 28 bytes decoded | base64 of 28-byte array | Valid | Boundary (minimum valid) |
| 6 | Valid base64, > 28 bytes decoded | base64 of 100-byte array | Valid | Normal valid partition |

---

### 5.3 Services

---

#### 5.3.1 `CryptoService`

**File:** `Services/Implementations/CryptoService.cs`

**What to mock:** Nothing — `CryptoService` is self-contained. It uses real cryptographic algorithms (Argon2id, AES-256-GCM) and internal validators. We test the real implementation because:
1. Mocking crypto would defeat the purpose — we need to verify real encryption/decryption works.
2. Validators are internal collaborators, not external dependencies to isolate.

**Fixture: `CryptoServiceFixture` (recommended as `IClassFixture<CryptoServiceFixture>`)**

**Why a fixture:** `CryptoService` is stateless but its construction triggers validator instantiation. More importantly, `DeriveKey` is computationally expensive (Argon2 with 64 MB memory). A shared fixture with a pre-computed key avoids repeating the ~500ms key derivation in every test that needs an encryption key. The fixture provides:
- A shared `CryptoService` instance
- A pre-derived 32-byte key (from a known password + salt)
- The known password and salt used to derive it

**Why stateless service still benefits from a fixture:** Even though `CryptoService` has no mutable state, the Argon2 key derivation is expensive. A fixture amortises this cost across all tests in the class.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`DeriveKey(string, byte[])`** | **Unit test** | (1) Returns exactly 32 bytes for any input. (2) Same password + salt → same key (determinism). (3) Different passwords → different keys. (4) Different salts → different keys. These are functional properties of a deterministic algorithm — straightforward unit tests. |
| **`Encrypt(string, byte[])`** | **Equivalence class + Unit test** | Inputs partition into: (1) valid plaintext + valid key → success result with blob, (2) empty/null plaintext → fail (via validator), (3) wrong key size → fail (via validator). The valid path also has a randomness property to verify (two encryptions of the same plaintext produce different nonces). |
| **`Decrypt(EncryptedBlob, byte[])`** | **Equivalence class + Unit test** | Inputs partition into: (1) correct blob + correct key → success, (2) wrong key → fail (AES-GCM auth failure), (3) tampered ciphertext → fail, (4) tampered tag → fail, (5) tampered nonce → fail, (6) null blob → fail (via validator), (7) wrong key size → fail (via validator). |
| **`Encrypt` → `Decrypt` round-trip** | **Slice test** | This is the most important test for `CryptoService`. It tests a vertical slice: encrypt plaintext → get blob → decrypt blob → get plaintext back. It catches integration issues between encrypt and decrypt that per-method tests miss (e.g., nonce/tag ordering). |
| **`GenerateSalt()`** | **Unit test** | Verify: (1) returns 16 bytes, (2) two calls produce different values (randomness). Simple property checks. |
| **`GeneratePassword(PasswordOptions)`** | **Decision table + ECT + BVA** | The method takes `PasswordOptions` with 5 booleans + 1 int. This is a combinatorial input space. A **decision table** is the right tool for the boolean flags (see Section 6.2). **BVA** applies to the length parameter (11, 12, 128, 129). **ECT** applies to the ExcludeAmbiguous flag (verify no ambiguous chars when true). |

##### `GeneratePassword` — detailed test cases

| # | Test | Technique | Why |
|---|------|-----------|-----|
| 1 | Default options → length 20, contains upper + lower + digit + special | Unit | Happy path |
| 2 | Length = 12 (min boundary) | BVA | Boundary |
| 3 | Length = 128 (max boundary) | BVA | Boundary |
| 4 | Length = 11 → Fail | BVA | Below boundary |
| 5 | Length = 129 → Fail | BVA | Above boundary |
| 6 | Only uppercase = true, rest false | Decision table | Single character set |
| 7 | Only lowercase = true | Decision table | Single character set |
| 8 | Only digits = true | Decision table | Single character set |
| 9 | Only special = true | Decision table | Single character set |
| 10 | All flags false → Fail | Decision table | No character set |
| 11 | All true + ExcludeAmbiguous → no ambiguous chars | Decision table + ECT | Ambiguous exclusion |
| 12 | Run 10 times → all results are unique (randomness) | Unit | Statistical property |

---

#### 5.3.2 `SessionService`

**File:** `Services/Implementations/SessionService.cs`

**What to mock:** Nothing — `SessionService` is self-contained. It uses only `System.Timers.Timer` internally.

**Fixture:** None (but use `IDisposable` on the test class to dispose the `SessionService` after each test).

**Why no shared fixture:** `SessionService` is **stateful** — it has mutable internal state (`_derivedKey`, `_currentUserId`, `_disposed`, etc.). Each test must start with a **fresh instance** to avoid test coupling. If tests shared a fixture, one test's `ClearSession()` or `Dispose()` would corrupt another test's state. Instead, each test creates its own `SessionService` and disposes it.

**Reasoning for strategy choice:** `SessionService` has a clear **state machine**:
```
[Created] → SetDerivedKey → [Active] → ClearSession → [Locked]
                                ↓                        ↓
                            Dispose →  [Disposed]  ←  Dispose
```
This makes **state-based testing** the primary technique, supplemented by **OOP testing** for the `IDisposable` contract and event mechanism.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`SetDerivedKey(byte[])`** | **State-based + ECT** | Transitions from Created → Active. ECT for the key parameter: (1) valid key → state becomes Active, (2) null → `ArgumentNullException`, (3) second call clears old key. State-based because the method changes internal state. |
| **`GetDerivedKey()`** | **State-based** | Behaviour depends on current state: (1) in Active state → returns clone of key, (2) in Created/Locked state → throws `InvalidOperationException`, (3) in Disposed state → throws `ObjectDisposedException`. Must test in each state. |
| **`SetUser(Guid, string, string?)`** | **Unit test + ECT** | Sets three fields. ECT: (1) valid inputs, (2) null email → `ArgumentNullException`. Not state-dependent (can set user in any non-disposed state). |
| **`GetAccessToken()`** | **State-based** | Returns token or null depending on whether `SetUser` was called. After dispose → `ObjectDisposedException`. |
| **`CurrentUserId` / `CurrentUserEmail`** | **State-based** | Null before `SetUser`, populated after, null again after `ClearSession`, throws after `Dispose`. |
| **`ClearSession()`** | **State-based** | Transitions Active → Locked. Verify: key zeroed, user/email/token cleared, `IsActive()` returns false, `VaultLocked` event fires. After dispose → returns silently (graceful). |
| **`IsActive()`** | **State-based** | False in Created, true after `SetDerivedKey`, false after `ClearSession`, throws after `Dispose`. |
| **`ResetInactivityTimer()`** | **State-based** | Can only call when not disposed. Verify timer restarts. |
| **`InactivityTimeout` property** | **ECT + BVA** | Setting to positive value → success. Setting to `TimeSpan.Zero` or negative → `ArgumentOutOfRangeException`. BVA at zero boundary. |
| **Inactivity timer expiry** | **State-based** | Set a short timeout (e.g., 100ms), set key, wait for timeout → verify `ClearSession` was called and `VaultLocked` fired. |
| **`Dispose()`** | **OOP test (IDisposable)** | (1) After dispose, all methods throw `ObjectDisposedException` (except `ClearSession` which returns gracefully). (2) Double dispose does not throw. |
| **`VaultLocked` event** | **OOP test (events)** | Subscribe to event, trigger `ClearSession`, verify handler was called. |
| **Key clone verification** | **OOP test (encapsulation)** | Call `GetDerivedKey`, modify the returned array, call `GetDerivedKey` again → verify internal key was not modified. |

---

#### 5.3.3 `VaultService`

**File:** `Services/Implementations/VaultService.cs`

**What to mock:**
- `ICryptoService` — to control encryption/decryption results without real crypto
- `ISessionService` — to control active/inactive state, user ID, derived key
- `IVaultRepository` — to control what entities are "in the database" without a real DB
- `ILogger<VaultService>` — to avoid null references (use `Mock<ILogger<VaultService>>()`)

**Why mock all 4 dependencies:** `VaultService` is an orchestrator — it coordinates crypto, session, and repository. Unit testing it means isolating its logic (state checks, error handling, data flow) from the real implementations of those dependencies. Mock return values let us test every code path.

**Fixture: `VaultServiceTestFixture` (recommended)**

**Why a fixture:** Every test needs the same 4 mocks set up in similar ways. A fixture encapsulates the creation of `Mock<ICryptoService>`, `Mock<ISessionService>`, `Mock<IVaultRepository>`, `Mock<ILogger<VaultService>>`, and a `VaultService` instance wired to them. Each test then configures mock behaviour specific to its scenario.

**Alternatively:** Use a helper method in the test class (not `IClassFixture`) because mocks need per-test configuration. A **shared setup method** (constructor or helper) that creates fresh mocks and a fresh `VaultService` for each test is the best approach.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`GetAllEntriesAsync()`** | **Unit test** | (1) Session inactive → returns Fail. (2) Session active, repository returns entities, crypto decrypts → returns entries. (3) Some entities fail decryption → those are skipped. Mock `ISessionService.IsActive()` and `IVaultRepository.GetAllEntriesAsync()` to control inputs. Straightforward path testing. |
| **`GetEntryAsync(string)`** | **ECT + Unit test** | Input `id` partitions into: (1) invalid GUID string → Fail, (2) valid GUID, entity not found → Fail, (3) valid GUID, entity found, decryption succeeds → Ok, (4) decryption fails → Fail, (5) repository throws → Fail. ECT is appropriate because the string input has distinct valid/invalid partitions. |
| **`AddEntryAsync(VaultEntry)`** | **Unit test** | Multiple code paths: (1) session inactive → Fail, (2) no user → Fail, (3) new entry (empty Id/default CreatedAt) → generates new Id/timestamps, (4) existing entry → preserves Id/CreatedAt, (5) encryption fails → Fail, (6) repository throws → Fail, (7) happy path → Ok. |
| **`DeleteEntryAsync(string)`** | **ECT + Unit test** | Input partitions: (1) session inactive → Fail, (2) invalid GUID → Fail, (3) valid GUID + repository succeeds → Ok, (4) repository throws → Fail. |
| **`SearchEntries(string, List<VaultEntry>)`** | **ECT** | This is a pure function (no dependencies) — no mocking needed. Partitions: (1) null/empty/whitespace query → returns all, (2) query matches WebsiteName → filtered, (3) matches Username → filtered, (4) matches Url → filtered, (5) matches Notes → filtered, (6) matches Category → filtered, (7) no match → empty list, (8) case-insensitive match, (9) query with leading/trailing whitespace. ECT is ideal because the function is a filter with clear input/output partitions. |

---

#### 5.3.4 `UserProfileService`

**File:** `Services/Implementations/UserProfileService.cs`

**What to mock:**
- `IVaultRepository` — to control database behaviour without a real DB

**Why mock:** `UserProfileService` is a thin wrapper around `IVaultRepository` that adds exception handling. Mocking the repository lets us simulate success, `PostgrestException`, and generic exceptions.

**Fixture:** None needed — the mock is simple enough to set up per-test.

**Why no fixture:** Only one dependency, and each test needs different mock behaviour. The setup is 3 lines of code.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`CreateProfileAsync(UserProfileEntity)`** | **Unit test** | Three code paths: (1) repository succeeds → `Result.Ok()`, (2) repository throws `PostgrestException` → `Result.Fail` with DB error, (3) repository throws generic exception → `Result.Fail` with general error. Each path is triggered by configuring the mock. |
| **`GetProfileAsync(Guid)`** | **Unit test** | Four paths: (1) found → `Result.Ok(profile)`, (2) not found (null) → `Result.Fail("not found")`, (3) `PostgrestException` → `Result.Fail` with DB error, (4) generic exception → `Result.Fail`. |

---

#### 5.3.5 `AuthService`

**File:** `Services/Implementations/AuthService.cs`

**What to mock:**
- `Supabase.Client` — **PROBLEMATIC**: this is a concrete class, not an interface. It is difficult to mock directly because `Auth` is a property returning a concrete type with methods like `SignUp`, `SignIn`, `SignOut`.
- `ICryptoService` — to control key derivation and encryption
- `IUserProfileService` — to control profile retrieval
- `ISessionService` — to control session state
- `ISupabaseExceptionMapper` — to control exception mapping
- `ILogger<AuthService>` — to avoid nulls

**Fixture strategy:** Due to the `Supabase.Client` concrete dependency, full unit testing of `AuthService` is difficult. **Recommendation:** Focus unit tests on the testable private methods extracted through the public API where possible, and acknowledge that `AuthService` is a better candidate for **integration testing** in a future phase.

**What CAN be tested without Supabase:**
- `ValidateCredentials` logic (called by `RegisterAsync` and `LoginAsync`) — test via the public methods by providing invalid email/password (the validation happens before any Supabase calls)
- `IsLocked()` — requires mocking `Supabase.Client.Auth.CurrentSession`, which may be difficult
- `ChangeMasterPasswordAsync` — returns `Result.Fail("Not implemented")`, trivially testable

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`RegisterAsync(email, password)`** — validation path only | **ECT** | Provide invalid email → expect validation failure. Provide invalid password → expect validation failure. These tests exercise the validator without needing Supabase. ECT partitions: invalid email, too-short password, missing special chars, etc. |
| **`ChangeMasterPasswordAsync(current, new)`** | **Unit test** | Always returns `Result.Fail("Not implemented.")`. Trivial test. |
| **Full `RegisterAsync`/`LoginAsync` flows** | **Integration test (future)** | These require a Supabase connection or significant mocking setup. Recommend integration tests against a test Supabase instance. |

---

#### 5.3.6 `VaultRepository`

**File:** `Services/Implementations/VaultRepository.cs`

**What to mock:** N/A — `VaultRepository` directly uses `Supabase.Client`, which is a concrete class.

**Why NOT to unit test:** `VaultRepository` is a thin data-access layer. Every method is a one-liner delegating to `Supabase.Client`. Unit testing with mocks would just verify that mock methods were called — it would test mock setup, not actual behaviour. This is a textbook case for **integration testing** against a real (or test) database.

**Recommendation:** Skip unit testing for `VaultRepository`. Cover it in a future integration test phase with a test Supabase instance.

---

#### 5.3.7 `ZxcvbnPasswordStrengthChecker`

**File:** `Services/Implementations/ZxcvbnPasswordStrengthChecker.cs`

**What to mock:** Nothing — uses the `Zxcvbn.Core` library directly. We test the real implementation because the purpose is to verify the integration with the zxcvbn library and our label/feedback mapping.

**Fixture:** None — construction is trivial.

**Why no fixture:** Stateless, cheap to construct.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`CheckStrength(string)`** | **ECT** | Input partitions based on expected strength: (1) empty string → score 0, (2) weak password like `"password"` → low score (0–1), (3) strong random password → high score (3–4). ECT is appropriate because password strength naturally partitions into discrete categories. |
| **`GetStrengthLabel(int)`** | **ECT + BVA** | Maps integers to labels. Partitions: 0→"Very Weak", 1→"Weak", 2→"Fair", 3→"Strong", 4→"Very Strong", anything else→"Unknown". BVA at boundaries: -1 and 5 (both → "Unknown"). This is also a good candidate for `[Theory]` with `[InlineData]`. |
| **`GetFeedback(string)`** | **Unit test** | Verify it returns a non-null string. The content depends on the zxcvbn library, so we just verify the integration works without crashing. |

---

### 5.4 Extensions

#### 5.4.1 `VaultEntryExtensions`

**File:** `Extensions/VaultEntryExtensions.cs`

**What to mock:** Nothing — pure extension methods with no dependencies.

**Fixture:** None.

**Why no mock/fixture:** These are pure mapping functions. Input → output. No state, no I/O.

**Reasoning for strategy:** These are data-mapping methods. The primary concern is that all fields are mapped correctly and null fields are handled. ECT is appropriate for the null-handling path.

##### Method-by-Method Strategy

| Method | Technique | Reasoning |
|--------|-----------|-----------|
| **`ToPayload(VaultEntry)`** | **Unit test + ECT** | Unit test: verify all 7 fields map correctly. ECT: one test with all populated fields (valid class), one with null fields (verify they default to `""`). |
| **`ToVaultEntry(VaultEntryPayload, Guid, DateTime, DateTime)`** | **Unit test + ECT** | Same approach: verify all fields map, plus metadata (Id, CreatedAt, UpdatedAt). ECT: null payload fields → default to `""`. |
| **`ToVaultEntry(VaultEntryPayload, VaultEntryEntity)`** | **Unit test** | Verify it delegates to the 3-parameter overload with `entity.Id`, `entity.CreatedAt`, `entity.UpdatedAt`. |
| **Round-trip: `ToPayload` → `ToVaultEntry`** | **Slice test** | Create a `VaultEntry`, convert to payload, convert back with metadata. Verify all fields survived. This catches field-mapping inconsistencies. |

---

### 5.5 Exception Mapper

#### 5.5.1 `SupabaseExceptionMapper`

**File:** `Exceptions/SupabaseExceptionMapper.cs`

**What to mock:** Nothing — the mapper is a pure function (exception → Result).

**Fixture:** None.

**Why decision table is the right technique:** The mapper has a clear decision structure: first pattern-match on exception type, then for `GotrueException`, match on status code, then fall back to message content matching. This is a multi-condition decision problem — the textbook use case for a decision table.

##### Method: `MapAuthException(Exception)`

**Decision table** (see [Section 6.3](#63-supabaseexceptionmapper)):

| # | Exception Type | Status Code | Message Content | Expected Result Message |
|---|---------------|-------------|-----------------|------------------------|
| 1 | `GotrueException` | 422 | (any) | "already exists" |
| 2 | `GotrueException` | 400 | (any) | "Invalid request" |
| 3 | `GotrueException` | 429 | (any) | "Too many attempts" |
| 4 | `GotrueException` | other | contains "already registered" | "already exists" |
| 5 | `GotrueException` | other | contains "user_already_exists" | "already exists" |
| 6 | `GotrueException` | other | contains "invalid" | "Invalid email or password" |
| 7 | `GotrueException` | other | other message | "Authentication failed" |
| 8 | `HttpRequestException` | — | — | "Network error" |
| 9 | Other exception | — | — | "unexpected error" |

---

## 6. Decision Tables

### 6.1 PasswordOptionsValidator

| Rule | Length in [12, 128]? | ≥ 1 char type selected? | Expected |
|------|---------------------|------------------------|----------|
| R1 | Yes | Yes | Valid |
| R2 | Yes | No (all false) | Invalid – "At least one character set" |
| R3 | No (< 12) | Yes | Invalid – "length must be between" |
| R4 | No (> 128) | Yes | Invalid – "length must be between" |
| R5 | No | No | Invalid – both errors |

### 6.2 CryptoService.GeneratePassword

Flags: `IncludeUppercase (U)`, `IncludeLowercase (L)`, `IncludeDigits (D)`, `IncludeSpecialCharacters (S)`, `ExcludeAmbiguousCharacters (A)`

| Rule | U | L | D | S | A | Expected |
|------|---|---|---|---|---|----------|
| R1 | T | T | T | T | F | All four char types present |
| R2 | T | T | T | T | T | All four types, no ambiguous chars |
| R3 | T | F | F | F | F | Uppercase only |
| R4 | F | T | F | F | F | Lowercase only |
| R5 | F | F | T | F | F | Digits only |
| R6 | F | F | F | T | F | Special chars only |
| R7 | F | F | F | F | F | Fail – no char types (caught by validator) |
| R8 | T | T | F | F | F | Upper + lower only |
| R9 | T | F | T | F | T | Upper + digits, no ambiguous |
| R10 | F | T | F | T | T | Lower + special, no ambiguous |

### 6.3 SupabaseExceptionMapper

| Rule | Exception Type | Status Code | Message Contains | Result |
|------|---------------|-------------|-----------------|--------|
| R1 | GotrueException | 422 | — | "already exists" |
| R2 | GotrueException | 400 | — | "Invalid request" |
| R3 | GotrueException | 429 | — | "Too many attempts" |
| R4 | GotrueException | other | "already registered" | "already exists" |
| R5 | GotrueException | other | "user_already_exists" | "already exists" |
| R6 | GotrueException | other | "invalid" | "Invalid email or password" |
| R7 | GotrueException | other | other | "Authentication failed" |
| R8 | HttpRequestException | — | — | "Network error" |
| R9 | (other) | — | — | "unexpected error" |

---

## 7. Equivalence Class Partitions

### 7.1 Password Validation

| Partition | Representative | Expected |
|-----------|---------------|----------|
| **Valid** (12–128 chars, all types) | `"Abcdefgh1!xy"` | Valid |
| **Empty** | `""` | Invalid – "cannot be empty" |
| **Too short** (1–11 chars) | `"Ab1!"` | Invalid – min length |
| **Too long** (> 128 chars) | 129-char string | Invalid – max length |
| **Missing uppercase** (12+ chars) | `"abcdefgh1!xy"` | Invalid – "must contain uppercase" |
| **Missing lowercase** (12+ chars) | `"ABCDEFGH1!XY"` | Invalid – "must contain lowercase" |
| **Missing digit** (12+ chars) | `"Abcdefghij!x"` | Invalid – "must contain digit" |
| **Missing special** (12+ chars) | `"Abcdefghij1x"` | Invalid – "must contain special" |

### 7.2 Email Validation

| Partition | Representative | Expected |
|-----------|---------------|----------|
| **Valid** | `"user@example.com"` | Valid |
| **Empty** | `""` | Invalid |
| **No @** | `"userexample.com"` | Invalid |
| **No domain** | `"user@"` | Invalid |
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
| **Valid (28+ bytes decoded)** | base64 of 28-byte or 100-byte array | Valid |
| **Empty** | `""` | Invalid |
| **Null** | `null` | Invalid |
| **Non-base64** | `"%%%"` | Invalid |
| **Valid base64, too short** | base64 of 27-byte array | Invalid |

---

## 8. OOP Testing Considerations

### 8.1 Inheritance Testing

- **`Result<T>` extends `Result`**: Verify polymorphic assignment — a `Result<int>` assigned to a `Result` variable should expose `Success` and `Message` correctly.
- **`VaultEntryEntity` / `UserProfileEntity` extend `BaseModel`**: Verify they can be used where `BaseModel` is expected.

### 8.2 Interface Contract Testing

| Interface | Implementation | Key Contract |
|-----------|---------------|-------------|
| `ICryptoService` | `CryptoService` | Encrypt → Decrypt round-trip recovers plaintext |
| `ISessionService` | `SessionService` | Set → Get → Clear lifecycle; `IDisposable` contract |
| `IPasswordStrengthChecker` | `ZxcvbnPasswordStrengthChecker` | Score is 0–4; labels match |
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

- **`SessionService.GetDerivedKey()` returns a clone**: Modify the returned array → call `GetDerivedKey()` again → verify internal state unchanged.
- **`CryptoService.DeriveKey`**: Verify that the password bytes are cleared from memory (the implementation does this, but it is hard to test externally — this is more of a code review concern).

### 8.5 Event Testing

- **`SessionService.VaultLocked` event**: Subscribe, call `ClearSession()`, verify the event handler was invoked with correct sender and `EventArgs.Empty`.

---

## 9. Additional Recommended Techniques

### 9.1 Boundary Value Analysis (BVA)

Key boundaries across the codebase:

| Parameter | Min | Min−1 | Max | Max+1 |
|-----------|-----|-------|-----|-------|
| Password length | 12 | 11 | 128 | 129 |
| Encryption key size | 32 | 31 | 32 | 33 |
| Email length | 1 | 0 | 256 | 257 |
| Nonce size | 12 | 11 | 12 | 13 |
| Tag size | 16 | 15 | 16 | 17 |
| Generated password length | 12 | 11 | 128 | 129 |

### 9.2 State Transition Testing (`SessionService`)

```
[Created] ──SetDerivedKey()──► [Active] ──ClearSession()──► [Locked]
                                  │                            │
                                  ├───Dispose()──► [Disposed]  ├──SetDerivedKey()──► [Active]
                                                       ▲
                               [Locked] ──Dispose()────┘
```

Test each valid transition. Test that invalid operations in each state produce correct errors.

### 9.3 Slice Testing Summary

| Slice | Components Involved | What It Verifies |
|-------|-------------------|-----------------|
| Encrypt → Decrypt | `CryptoService.Encrypt`, `CryptoService.Decrypt` | Round-trip data integrity |
| Blob serialisation | `EncryptedBlob.ToBase64String`, `EncryptedBlob.FromBase64String` | Serialisation/deserialisation consistency |
| Payload serialisation | `VaultEntryPayload.ToJson`, `VaultEntryPayload.FromJson` | JSON round-trip |
| Entry mapping | `VaultEntry` → `ToPayload` → `ToVaultEntry` | Field mapping consistency |
| Full vault flow | `VaultEntry` → `ToPayload` → `ToJson` → `Encrypt` → `ToBase64String` → `FromBase64String` → `Decrypt` → `FromJson` → `ToVaultEntry` | Complete data pipeline integrity |

### 9.4 Pairwise / Combinatorial Testing

`PasswordOptions` has 5 boolean flags + 1 integer. Full combination = 2^5 × many lengths = huge. Use pairwise testing to reduce while covering all 2-way interactions:

| U | L | D | S | A |
|---|---|---|---|---|
| T | T | T | T | F |
| T | F | F | F | T |
| F | T | F | T | F |
| F | F | T | F | T |
| T | T | F | F | F |
| F | F | F | T | T |

### 9.5 Error Guessing

| Area | Error Guess |
|------|------------|
| `CryptoService.DeriveKey` | Empty password, Unicode password (emoji), very long password |
| `CryptoService.Encrypt` | Unicode plaintext (emoji, CJK characters) |
| `EncryptedBlob.ToBase64String` | Blob with zero-length ciphertext |
| `VaultService.SearchEntries` | Entries with null properties, empty list |
| `SessionService` | Concurrent `SetDerivedKey` / `ClearSession` from different threads |

---

## 10. Coverage Goals & Measurement

### 10.1 Running Coverage

```bash
# Run tests with coverage
dotnet test PasswordManager.Tests/PasswordManager.Tests.csproj \
  --collect:"XPlat Code Coverage"

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
| **Line coverage** | ≥ 80% | All Core classes |
| **Branch coverage** | ≥ 75% | Important for validators and decision logic |
| **Method coverage** | 100% | Every public/internal method has ≥ 1 test |
| **Class coverage** | 100% | Every class has a test class |

### 10.3 Exclusions

Exclude from coverage metrics:
- `VaultRepository` — Supabase-dependent; integration test candidate
- `AuthService` — mostly Supabase-dependent; only validation logic is unit-testable

### 10.4 CI Enforcement

```bash
dotnet test /p:CollectCoverage=true /p:Threshold=80 /p:ThresholdType=line
```

---

## 11. Test Organisation & Naming Conventions

### 11.1 Directory Structure

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
├── Exceptions/
│   └── SupabaseExceptionMapperTests.cs
└── Fixtures/
    └── CryptoServiceFixture.cs
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

### 11.3 Test Attributes

| Attribute | Use For |
|-----------|---------|
| `[Fact]` | Single deterministic test case |
| `[Theory]` + `[InlineData(...)]` | Parameterised tests (equivalence classes, decision tables) |
| `[Trait("Category", "UnitTest")]` | Categorise tests |
| `[Trait("Category", "BoundaryValue")]` | Tag boundary tests |
| `[Trait("Category", "DecisionTable")]` | Tag decision table tests |
| `[Trait("Category", "SliceTest")]` | Tag slice / integration-style tests |

### 11.4 AAA Pattern

Every test follows Arrange–Act–Assert:

```csharp
// Arrange — create inputs, configure mocks
// Act — call the method under test
// Assert — verify the result
```

---

## Summary: Fixtures, Mocks, and Strategy by Component

| Component | Fixture? | Mocks? | Primary Strategy | Why |
|-----------|----------|--------|-----------------|-----|
| `Result` / `Result<T>` | No | No | Unit test + OOP | Simple factories; test polymorphism |
| `EncryptedBlob` | No | No | ECT + BVA + Slice | Input partitions for `FromBase64String`; round-trip slice |
| `PasswordOptions` | No | No | Unit test | Pure POCO defaults |
| `VaultEntry` | No | No | Unit test + OOP | POCO + init-only verification |
| `VaultEntryPayload` | No | No | ECT + Slice | JSON parse partitions; round-trip slice |
| `PasswordPolicy` | No | No | Unit test | Constants verification |
| All 6 Validators | No | No | **ECT + BVA** | Validators are classifiers — ECT is the natural fit |
| `PasswordOptionsValidator` | No | No | **Decision table** | Boolean conditions create a truth table |
| `CryptoService` | **Yes** (`CryptoServiceFixture`) | No | Unit + Decision table + Slice | Fixture for expensive key derivation; decision table for `GeneratePassword` flags; slice for encrypt/decrypt round-trip |
| `SessionService` | No (fresh per test) | No | **State-based + OOP** | Stateful service with lifecycle; IDisposable contract |
| `VaultService` | Helper setup | **Yes** (4 mocks) | Unit test + ECT | Orchestrator — mock all deps to test logic in isolation |
| `UserProfileService` | No | **Yes** (1 mock) | Unit test | Thin wrapper — mock repository for exception paths |
| `ZxcvbnPasswordStrengthChecker` | No | No | ECT + BVA | Strength categories are equivalence classes |
| `SupabaseExceptionMapper` | No | No | **Decision table** | Status code × message content → output mapping |
| `VaultEntryExtensions` | No | No | Unit + ECT + Slice | Pure mappers; null handling; round-trip |
| `AuthService` | — | Limited | ECT (validation only) | Supabase dependency limits unit testing |
| `VaultRepository` | — | — | **Skip (integration test)** | Pure data access; mocking adds no value |

**Total estimated test cases: ~170**
