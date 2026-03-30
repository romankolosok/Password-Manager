# Crypty — Secure Password Manager

> A cross-platform desktop password manager built on end-to-end encryption. Your vault data is encrypted on your device before it ever leaves — the server never sees your master password or plaintext credentials.

![Crypty Logo](https://assets.crypty.cc/crypty-logo.png)

---

## Table of Contents

1. [Overview](#overview)
2. [Features](#features)
3. [Security Architecture](#security-architecture)
4. [Tech Stack](#tech-stack)
5. [Getting Started](#getting-started)
6. [Roadmap](#roadmap)
7. [Contributing](#contributing)
   - [Project Structure](#project-structure)
   - [Developer Setup](#developer-setup)
   - [Running Tests](#running-tests)
   - [Contributing Guidelines](#contributing-guidelines)
8. [License](#license)
9. [Support](#support)

---

## Overview

**Crypty** is a desktop password manager that stores your credentials in a cloud-synced, fully encrypted vault. It is built with a zero-knowledge design: your master password never leaves your device, and all encryption and decryption happens locally. The backend (Supabase) only ever stores encrypted ciphertext.

The application handles the full authentication lifecycle — account creation with email OTP verification, login, inactivity-based auto-lock, master password change, and an account recovery flow using a one-time recovery key.

---

## Features

| Feature | Description |
|---|---|
| **End-to-end encrypted vault** | Every credential is encrypted with AES-256-GCM before being sent to the server |
| **Zero-knowledge master password** | Derived locally via Argon2id; never transmitted |
| **Data Encryption Key (DEK)** | A per-account key is encrypted with your master password; re-encryption on password change leaves vault data untouched |
| **Account recovery key** | A randomly generated recovery key lets you regain access if you forget your master password |
| **Email OTP verification** | New accounts are verified via a one-time code sent to email |
| **Password generator** | Configurable generator (length, uppercase, digits, symbols) with real-time zxcvbn strength scoring |
| **Inactivity auto-lock** | Vault automatically locks after a configurable idle timeout (default 5 minutes) |
| **Clipboard auto-clear** | Copied passwords are cleared from the clipboard after a configurable delay (default 15 seconds) |
| **Search** | Instant client-side search across all vault entries |
| **Fluent UI** | Modern Avalonia desktop UI with the Fluent theme and Inter font |

---

## Security Architecture

```
Master Password
      │
      ▼ Argon2id KDF
      │  (64 MB memory, 3 iterations, 4 threads)
      │  + per-user Salt (stored in UserProfiles)
      ▼
 Master Key (256-bit)
      │
      ├──► Decrypt EncryptedDEK  →  Data Encryption Key (DEK)
      │         (AES-256-GCM)
      │
      └──► DEK used to encrypt/decrypt each VaultEntry
                (AES-256-GCM, unique nonce per entry)
```

- The **master password** is never stored or transmitted.
- The **salt** and **encrypted DEK** are stored in the `UserProfiles` table; only the authenticated user can read them (Postgres RLS).
- Each `VaultEntry` stores only `EncryptedData` — a JSON-serialised `EncryptedBlob` containing `nonce`, `ciphertext`, and GCM `tag`.
- A **recovery key** optionally stores the DEK encrypted under a separate salt, enabling account recovery without the master password.
- Sensitive byte arrays (plaintext, derived keys) are zeroed in memory after use.

---

## Tech Stack

| Category | Technology |
|---|---|
| **Language / Runtime** | C# 12, .NET 8 |
| **UI Framework** | [Avalonia](https://avaloniaui.net/) 11.x (Fluent theme, SVG/Skia) |
| **MVVM** | CommunityToolkit.Mvvm 8.4 |
| **Backend / Database** | [Supabase](https://supabase.com/) — Postgres, GoTrue auth, PostgREST |
| **Key Derivation** | Argon2id via [Konscious.Security.Cryptography](https://github.com/kmaragon/Konscious.Security.Cryptography) |
| **Symmetric Encryption** | AES-256-GCM (.NET `System.Security.Cryptography`) |
| **Input Validation** | FluentValidation 12 |
| **Password Strength** | [zxcvbn-core](https://github.com/trichards57/zxcvbn-cs) |
| **Logging** | Serilog (file sink) |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection |
| **Configuration** | Microsoft.Extensions.Configuration (JSON + environment variables) |
| **Testing** | xUnit, FluentAssertions, Moq, Bogus, Coverlet |

---

## Getting Started

Crypty is a pre-configured application — no accounts, servers, or setup are required beyond downloading and running the installer.

1. Go to the [**Releases**](../../releases) page and download the latest installer for your platform.
2. Run the installer and launch **Crypty**.
3. Click **Register** to create a free account using your email address.
4. Verify your email with the one-time code that is sent to you.
5. Set a strong **master password** — this is the only password you need to remember. It is never transmitted; all encryption happens on your device.
6. Save the **recovery key** that is shown to you after registration. Store it somewhere safe — it is the only way to recover your account if you forget your master password.

That's it. Your vault is ready.

### Using the Vault

![Crypty Vault View](https://assets.crypty.cc/crypty-vault-list.png)

| Action | How |
|---|---|
| **Add an entry** | Click the **+** button and fill in the website, username, and password |
| **Copy a password** | Click the copy icon next to any entry — it is cleared from the clipboard automatically after 15 seconds |
| **Copy a username** | Click the copy icon next to the username field |
| **Edit an entry** | Click the entry in the list to open the detail view |
| **Delete an entry** | Open the entry and click **Delete**, then confirm |
| **Search** | Type in the search bar — results filter instantly |
| **Generate a password** | Use the password generator inside the entry detail view; adjust length and character rules and check the live strength score |
| **Lock the vault** | Click your account menu (top-right) and select **Lock**, or simply walk away — the vault locks automatically after 5 minutes of inactivity |
| **Change master password** | Account menu → **Change Password** |

---

## Roadmap
- [ ] **Browser extension companion** — auto-fill credentials in browsers
- [ ] **TOTP / 2FA storage** — store and generate TOTP codes alongside credentials
- [ ] **Import / Export** — support CSV and common password manager formats (Bitwarden, KeePass)
- [ ] **Tags and folders** — organise vault entries into categories
- [ ] **Breach monitoring** — check stored passwords against Have I Been Pwned
- [x] **CI/CD pipeline** — automated build and test on push

---

## Contributing

### Project Structure

```
PasswordManager/
├── PasswordManager.sln
│
├── PasswordManager.Core/          # Domain logic — no UI dependencies
│   ├── Services/
│   │   ├── Interfaces/            # IAuthService, ICryptoService, IVaultService, …
│   │   └── Implementations/       # AuthService, CryptoService, VaultService, …
│   ├── Entities/                  # PostgREST-mapped DB entities
│   ├── Models/                    # Result<T>, VaultEntry, EncryptedBlob, …
│   ├── Validators/                # FluentValidation validators
│   ├── Exceptions/                # SupabaseExceptionMapper, AuthMessages
│   ├── Helpers/                   # Sanitizer
│   └── Extensions/                # VaultEntryExtensions
│
├── PasswordManager.App/           # Avalonia desktop application ("Crypty")
│   ├── ViewModels/                # LoginViewModel, VaultListViewModel, …
│   ├── Views/                     # .axaml views + code-behind
│   ├── Services/                  # AuthCoordinator, DialogService, ClipboardService
│   ├── Converters/                # Value converters (password masking, strength brush, …)
│   ├── Assets/                    # Icons, fonts
│   ├── App.axaml.cs               # DI composition root; Supabase client setup
│   ├── appsettings.json           # Baked-in production backend config (not for dev use)
│   └── appsettings.Development.json  # Your local Supabase overrides
│
├── PasswordManager.Tests/         # xUnit test suite
│   ├── Services/                  # Unit + integration tests per service
│   ├── Validators/                # Validator tests
│   ├── Models/                    # Model tests
│   ├── Fixtures/                  # Shared test fixtures (Supabase, Crypto, …)
│   └── Helpers/                   # InbucketClient (email OTP in integration tests)
│
└── supabase/                      # Supabase CLI project
    ├── config.toml
    ├── migrations/                # Ordered SQL migrations
    └── remote_schema.sql          # Reference snapshot of remote schema
```

---

### Developer Setup

The production backend is not accessible to contributors — you run the full stack locally via the Supabase CLI and Docker.

**Prerequisites**

| Requirement | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 or later |
| [Supabase CLI](https://supabase.com/docs/guides/cli) | Latest |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Required by the Supabase CLI |

**Steps**

1. **Clone the repository**

   ```bash
   git clone https://github.com/<your-org>/PasswordManager.git
   cd PasswordManager/PasswordManager
   ```

2. **Start the local Supabase stack** (Docker must be running)

   ```bash
   supabase start
   ```

   The CLI prints the local credentials. Default endpoints:

   | Service | URL |
   |---|---|
   | API | `http://127.0.0.1:54321` |
   | Studio (DB explorer) | `http://127.0.0.1:54323` |
   | Inbucket (email inbox) | `http://127.0.0.1:54324` |

3. **Apply the migrations**

   ```bash
   supabase db reset
   ```

4. **Update `appsettings.Development.json`** with the credentials printed by the CLI

   ```json
   {
     "Supabase": {
       "Url": "http://127.0.0.1:54321",
       "AnonKey": "<anon key from supabase start output>",
       "ServiceRoleKey": "<service role key from supabase start output>"
     },
     "Session": {
       "InactivityTimeoutMinutes": 5
     },
     "Clipboard": {
       "AutoClearSeconds": 15
     }
   }
   ```

5. **Run the app against the local stack**

   ```powershell
   # PowerShell
   $env:DOTNET_ENVIRONMENT = "Development"
   dotnet run --project PasswordManager.App
   ```

   ```bash
   # bash / zsh
   DOTNET_ENVIRONMENT=Development dotnet run --project PasswordManager.App
   ```

6. **View emails** (OTP codes during registration / password reset) at `http://127.0.0.1:54324`.

> **Note:** `appsettings.json` contains the baked-in production backend config shipped with the release build. Do not edit it for development purposes — use `appsettings.Development.json` instead, which is gitignored and loaded only when `DOTNET_ENVIRONMENT=Development`.

---

### Running Tests

The test suite has **unit tests** (no dependencies) and **integration tests** (require the local Supabase stack from the steps above).

**Unit tests only**

```bash
dotnet test PasswordManager.Tests --filter "Category!=Integration"
```

**All tests (unit + integration)**

```bash
# Supabase must already be running (supabase start)
$env:DOTNET_ENVIRONMENT = "Development"   # PowerShell
dotnet test PasswordManager.Tests
```

**With code coverage**

```bash
dotnet test PasswordManager.Tests `
  /p:CollectCoverage=true `
  /p:CoverletOutputFormat=opencover `
  /p:CoverletOutput=./coverage/
```

JUnit XML results are written automatically to the output directory.

---

### Contributing Guidelines

1. Fork the repository and create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. Follow the existing code style — nullable reference types enabled, no redundant comments, CommunityToolkit.Mvvm patterns for ViewModels, `Result<T>` for all service return values.
3. Add or update tests for any changed behaviour.
4. Ensure all tests pass before opening a pull request.
5. Open a pull request with a clear description of the change and why it is needed.

Please report bugs and request features via [GitHub Issues](../../issues).

---

## License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for the full text.

---

## Support

- **Bug reports & feature requests**: [Open a GitHub Issue](../../issues)
- **Questions**: Use the [GitHub Discussions](../../discussions) tab
- **Security vulnerabilities**: Please do **not** open a public issue. Contact the maintainer directly via the email listed on the GitHub profile.

---

*Built with .NET 8, Avalonia, and Supabase.*
