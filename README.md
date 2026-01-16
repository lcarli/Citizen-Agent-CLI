# CitizenAgent Setup CLI

A .NET CLI tool for creating and configuring Citizen Agent resources.

## Features

The CLI provides comprehensive tooling for Citizen Agent setup:

| Command | Description |
|---------|-------------|
| `setup` | Complete setup wizard - runs all phases |
| `blueprint create` | Create Blueprint App Registration |
| `identity create` | Create Agent Identity |
| `user create` | Create Agent User |
| `permissions grant` | Create OAuth2 permission grants |
| `config init` | Generate configuration file template |
| `config validate` | Validate configuration file |

## Installation

### Build from Source

```bash
cd CitizenAgent-CLI
dotnet build
dotnet run -- --help
```

### Install as Global Tool

```bash
dotnet pack
dotnet tool install --global --add-source ./nupkg CitizenAgent.Setup.Cli
```

## Usage

### Quick Start (Recommended)

**Step 1:** Login via Azure CLI (required):

```bash
az login
```

**Step 2:** Generate a configuration file:

```bash
ca-setup config init
```

**Step 3:** Edit `citizen-agent.config.json` with your values:
- `tenantId` - Your Azure AD tenant ID
- `blueprintDisplayName` - Name for the Blueprint app
- `agentIdentityDisplayName` - Name for the Agent Identity
- `agentUserUpn` - UPN for the Agent User (e.g., myagent@contoso.com)

**Step 4:** Run the complete setup:

```bash
ca-setup setup
```

That's it! The CLI authenticates via Azure CLI and creates all resources automatically.

### Individual Commands

#### Create Blueprint Only

```bash
ca-setup blueprint create \
  --tenant-id "..." \
  --name "MyAgent-Blueprint"
```

#### Create Agent Identity

```bash
ca-setup identity create \
  --tenant-id "..." \
  --name "MyAgent-Identity" \
  --blueprint-app-id "..." \
  --blueprint-secret "..."
```

#### Create Agent User

```bash
ca-setup user create \
  --tenant-id "..." \
  --upn "myagent@contoso.com" \
  --display-name "My Agent" \
  --identity-id "..."
```

#### Create Permission Grant

```bash
ca-setup permissions grant \
  --tenant-id "..." \
  --identity-id "..." \
  --user-id "..."
```

## Prerequisites

### Azure CLI Authentication

The CLI uses interactive authentication via Azure CLI. Before using, login with an account that has admin privileges:

```bash
az login
```

Your account must have one of these roles:
- **Global Administrator**
- **Application Administrator**
- **Cloud Application Administrator**

### Required Permissions

The authenticated user needs permissions to:
- Create App Registrations
- Create Service Principals
- Create Users
- Grant OAuth2 permissions

## Output

The CLI generates an output file (`CitizenAgent-output.json`) containing all created resource IDs:

```json
{
  "tenantId": "...",
  "blueprint": {
    "appId": "...",
    "objectId": "...",
    "servicePrincipalId": "...",
    "clientSecret": "..."
  },
  "agentIdentity": {
    "id": "..."
  },
  "agentUser": {
    "id": "...",
    "upn": "..."
  },
  "permissions": {
    "oauth2GrantId": "..."
  }
}
```

## Architecture

```
CitizenAgent.Setup.Cli/
├── Commands/           # CLI command implementations
│   ├── SetupCommand.cs
│   ├── BlueprintCommand.cs
│   ├── IdentityCommand.cs
│   ├── UserCommand.cs
│   ├── PermissionsCommand.cs
│   └── ConfigCommand.cs
├── Constants/          # Graph API constants and permission definitions
├── Exceptions/         # Custom exceptions with suggestions
├── Helpers/            # Logging and utility helpers
├── Models/             # Data models
└── Services/           # Service layer
    ├── GraphApiService.cs
    ├── InteractiveAuthService.cs
    ├── BlueprintService.cs
    ├── AgentIdentityService.cs
    ├── AgentUserService.cs
    ├── PermissionService.cs
    ├── LicenseService.cs
    ├── FoundryService.cs
    └── ConfigService.cs
```

## Design Principles

This CLI follows the patterns established by the official Agent365 DevTools CLI:

- **Interactive Authentication**: Uses Azure CLI or browser-based authentication (no client secrets needed)
- **Dependency Injection**: All services are injectable for testability
- **Structured Logging**: Uses `Microsoft.Extensions.Logging` with file and console output
- **Command Pattern**: Uses `System.CommandLine` for CLI parsing
- **Error Handling**: Custom exceptions with exit codes and suggestions
- **Idempotent Operations**: Checks for existing resources before creating
- **Progress Feedback**: Clear phase and step indicators

## License

MIT License - See LICENSE file for details.