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
cd custom_project/cli/CitizenAgent.Setup.Cli
dotnet build
dotnet run -- --help
```

### Install as Global Tool

```bash
dotnet pack
dotnet tool install --global --add-source ./nupkg CitizenAgent.Setup.Cli
```

## Usage

### Complete Setup (Recommended)

Run all phases in one command:

```bash
ca-setup setup \
  --tenant-id "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
  --blueprint-name "MyAgent-Blueprint" \
  --identity-name "MyAgent-Identity" \
  --user-upn "myagent@contoso.com" \
  --user-display-name "My Agent" \
  --client-id "11111111-2222-3333-4444-555555555555" \
  --client-secret "your-management-app-secret" \
  --foundry-resource "/subscriptions/.../providers/Microsoft.CognitiveServices/accounts/my-foundry" \
  --webhook-url "https://myagent.azurewebsites.net/webhook"
```

### Using Configuration File

1. Generate a configuration template:

```bash
ca-setup config init --output my-agent-config.json
```

2. Edit the configuration file with your values

3. Run setup with the configuration:

```bash
ca-setup setup --config my-agent-config.json
```

### Individual Commands

#### Create Blueprint Only

```bash
ca-setup blueprint create \
  --tenant-id "..." \
  --name "MyAgent-Blueprint" \
  --client-id "..." \
  --client-secret "..."
```

#### Create Agent Identity

```bash
ca-setup identity create \
  --tenant-id "..." \
  --name "MyAgent-Identity" \
  --blueprint-sp-id "..." \
  --client-id "..." \
  --client-secret "..."
```

#### Create Agent User

```bash
ca-setup user create \
  --tenant-id "..." \
  --upn "myagent@contoso.com" \
  --display-name "My Agent" \
  --identity-id "..." \
  --client-id "..." \
  --client-secret "..."
```

#### Create Permission Grant

```bash
ca-setup permissions grant \
  --tenant-id "..." \
  --identity-id "..." \
  --user-id "..." \
  --client-id "..." \
  --client-secret "..."
```

## Prerequisites

### Management App Registration

Before using this CLI, you must create a Management App Registration in Azure AD with the following **Application permissions** (with admin consent):

- `Application.ReadWrite.All`
- `User.ReadWrite.All`
- `DelegatedPermissionGrant.ReadWrite.All`
- `Directory.ReadWrite.All`
- `Organization.Read.All` (for license operations)

### Azure CLI (Optional)

For Foundry role assignment, you must be logged in via Azure CLI:

```bash
az login
```

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
    ├── BlueprintService.cs
    ├── AgentIdentityService.cs
    ├── AgentUserService.cs
    ├── PermissionService.cs
    ├── LicenseService.cs
    ├── FoundryService.cs
    └── ConfigService.cs
```

## Design Principles

This CLI follows the patterns established by the official CitizenAgent DevTools CLI:

- **Dependency Injection**: All services are injectable for testability
- **Structured Logging**: Uses `Microsoft.Extensions.Logging` with file and console output
- **Command Pattern**: Uses `System.CommandLine` for CLI parsing
- **Error Handling**: Custom exceptions with exit codes and suggestions
- **Idempotent Operations**: Checks for existing resources before creating
- **Progress Feedback**: Clear phase and step indicators

## License

MIT License - See LICENSE file for details.