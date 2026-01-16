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

## Prerequisites

Before using the CitizenAgent CLI, you must create a **custom Entra ID app registration** with specific Microsoft Graph API permissions.

### 1. Create Custom Client App Registration

1. Go to **Azure Portal** → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Enter:
   - **Name**: `CitizenAgent-CLI` (or your preferred name)
   - **Supported account types**: Single tenant (Accounts in this organizational directory only)
   - **Redirect URI**: Select **Public client/native (mobile & desktop)** → Enter `http://localhost`
3. Click **Register**
4. Copy the **Application (client) ID** - you'll need this later

### 2. Configure API Permissions

Add the following **Delegated permissions** (NOT Application permissions):

| Permission | Purpose |
|------------|---------|
| `Application.ReadWrite.All` | Create and manage Blueprint app registrations |
| `User.ReadWrite.All` | Create Agent Users |
| `DelegatedPermissionGrant.ReadWrite.All` | Grant OAuth2 permissions |
| `Directory.Read.All` | Read directory data for validation |

To add permissions:
1. In your app registration, go to **API permissions**
2. Click **Add a permission** → **Microsoft Graph** → **Delegated permissions**
3. Search and add each permission listed above
4. Click **Grant admin consent for [Your Tenant]**
5. Verify all permissions show green checkmarks ✓

> ⚠️ **Important**: Use **Delegated permissions** (you sign in, CLI acts on your behalf), NOT Application permissions.

### 3. Required Admin Roles

Your account must have one of these roles to grant admin consent:
- **Application Administrator** (recommended)
- **Cloud Application Administrator**
- **Global Administrator**

> 💡 **Why is this required?** The CLI needs elevated permissions to create and manage Agent Identity Blueprints in your tenant. You maintain control over which permissions are granted, and the app stays within your tenant's security boundaries.

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

**Step 1:** Login via Azure CLI:

```bash
az login
```

**Step 2:** Generate a configuration file:

```bash
ca-setup config init
```

**Step 3:** Edit `citizen-agent.config.json` with your values:
- `tenantId` - Your Azure AD tenant ID
- `clientAppId` - **The App ID from Prerequisites step 1**
- `blueprintDisplayName` - Name for the Blueprint app
- `agentIdentityDisplayName` - Name for the Agent Identity
- `agentUserUpn` - UPN for the Agent User (e.g., myagent@contoso.com)

**Step 4:** Run the complete setup:

```bash
ca-setup setup
```

A browser window will open for authentication. Sign in with your admin account and the CLI will create all resources automatically.

### Individual Commands

#### Create Blueprint Only

```bash
ca-setup blueprint create \
  --tenant-id "..." \
  --name "MyAgent-Blueprint" \
  --client-app-id "your-app-id"
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
  --identity-id "..." \
  --client-app-id "your-app-id"
```

#### Create Permission Grant

```bash
ca-setup permissions grant \
  --tenant-id "..." \
  --identity-id "..." \
  --user-id "..." \
  --client-app-id "your-app-id"
```

## Troubleshooting

### "This operation cannot be performed for the specified calling identity type"

This error means you're using Azure CLI authentication instead of the custom client app. Make sure:
1. You have created the custom app registration (see Prerequisites)
2. The `clientAppId` is set in your `citizen-agent.config.json`
3. Or pass `--client-app-id` on the command line

### "Insufficient privileges to complete the operation"

Ensure your custom app has all required delegated permissions with admin consent granted.

### Browser doesn't open for authentication

The CLI uses interactive browser authentication when a `clientAppId` is provided. Ensure:
1. Port 80 is not blocked by firewall
2. You have a default browser configured

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