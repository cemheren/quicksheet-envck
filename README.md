# quicksheet-envck

**Environment variable inspector for [QuickSheet](https://github.com/cemheren/QuickSheet).**

Inspect, filter, and safely view your environment variables directly on your desktop wallpaper or terminal spreadsheet. Sensitive values (API keys, tokens, passwords) are automatically masked.

## Install

```
ext: github:Deskworks/quicksheet-envck
```

## Usage

| Cell value | What it shows |
|---|---|
| `env: PATH` | Each PATH entry on its own row |
| `env: HOME` | Value of the HOME variable |
| `env: GITHUB_TOKEN` | Masked: `ghp_****` |
| `env: list` | All variables (sorted A–Z, sensitive masked) |
| `env: filter:GITHUB` | All `GITHUB_*` variables |
| `env: filter:AWS` | All `AWS_*` variables |
| `env: MY_VAR` | Value of any specific variable |

## Features

- 🔍 **Lookup any variable** — case-insensitive match
- 🔒 **Auto-masking** — keys, tokens, secrets, passwords shown as `****`
- 📋 **List all** — sorted alphabetical view of every variable
- 🔎 **Filter mode** — substring match on variable name
- 🛤️ **PATH exploder** — splits PATH into individual entries per row
- ⚠️ **Not-set indicator** — clear warning when a variable is missing
- Zero network calls, zero NuGet dependencies

## Use cases

```
# Check if your cloud CLI is configured
env: AWS_PROFILE
env: AZURE_SUBSCRIPTION_ID
env: KUBECONFIG

# Debug a missing tool
env: PATH

# Verify CI/CD secrets are injected (shows masked)
env: GITHUB_TOKEN
env: NPM_TOKEN

# Survey all AWS variables at once
env: filter:AWS
```

## Requires

- [QuickSheet](https://github.com/cemheren/QuickSheet)
- .NET 9 SDK

## Protocol

Implements the [QuickSheet extension protocol](https://github.com/cemheren/QuickSheet/blob/main/docs/extension-protocol.md) via JSON-lines stdin/stdout.

## License

MIT
