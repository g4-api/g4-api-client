# G4.Api

[![NuGet](https://img.shields.io/nuget/v/g4.api?logo=nuget&logoColor=959da5&label=NuGet&labelColor=24292f)](https://www.nuget.org/packages/g4.Api)
[![Build, Test & Release](https://github.com/g4-api/g4-api-client/actions/workflows/release-pipeline.yml/badge.svg)](https://github.com/g4-api/g4-api-client/actions/workflows/release-pipeline.yml)

**G4.Api** is the official client library for the **G4™ Engine**, providing seamless access to automation, environments, integrations, and templates management. Through its intuitive interfaces, you can quickly leverage the robust features of G4 to build, run, and manage automation workflows, environment parameters, and plugin integrations.

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Getting Started](#getting-started)
- [Basic Usage](#basic-usage)
- [Contributing](#contributing)
- [License](#license)

---

## Features

1. **Automation Management**
   - Invoking, tracking, and customizing automated workflows.
   - Handling job, rule, and stage-level events and callbacks.

2. **Environments Management**
   - Creating, deleting, and updating environment-specific parameters.
   - Decoding and encoding parameters for secure storage and retrieval.

3. **Integrations**
   - Centralized management of external repositories and plugin metadata.
   - Cached retrieval of plugin documents, manifests, and other artifacts.

4. **Templates**
   - Adding and removing plugin templates for reuse in automation scenarios.
   - Maintaining a dedicated LiteDB collection for template management.

---

## Installation

You can install **G4.Api** via NuGet:

```bash
# For latest version, visit: https://www.nuget.org/packages/G4.Api

dotnet add package G4.Api --version <LatestVersion>

# Or, if you prefer the latest version:

dotnet add package G4.Api
```

Or by adding it directly in your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="G4.Api" Version="<LatestVersion>" />
  </ItemGroup>
</Project>
```

---

## Getting Started

### 1. Reference the library

Add a reference to `G4.Api` in your .NET project. The library targets `net10.0`, so your project needs the .NET 10 SDK or a compatible target framework.

### 2. Create a G4Client

```csharp
using G4.Api;

var g4Client = new G4Client();
```

### 3. Explore the clients

`G4Client` exposes these public properties:

| Property | Type | What it does |
| --- | --- | --- |
| `Automation` | `IAutomationClient` | Manages and invokes automations. |
| `AutomationAsync` | `IAutomationAsyncClient` | Manages and invokes automations asynchronously, using an `IQueueManager`. |
| `Credentials` | `ICredentialsClient` | Manages credentials. |
| `Environments` | `IEnvironmentsClient` | Manages environments and their parameters. |
| `Flows` | `IFlowsClient` | Manages and invokes flows: complete automations persisted and cached by namespace and key. |
| `Integration` | `IIntegrationClient` | Handles integrations. |
| `Logger` | `ILogger` | The logger instance used by the client. |
| `Templates` | `ITemplateClient` | Manages plugin templates. |

### 4. Start automating

- Create or fetch environment parameters.
- Add or remove plugin templates.
- Invoke automations and handle events such as `RuleInvoking`, `JobInvoked`, and more.

---

## Basic Usage

Below is a simplified example demonstrating how you might use **G4.Api** to work with an environment parameter and trigger an automation:

```csharp
using G4.Api;
using Microsoft.Extensions.Logging;

// 1. Create the client
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("G4Logger");
var g4Client = new G4Client(logger);

// 2. Update environment parameters
var parameters = new Dictionary<string, string>
{
    { "ApiKey", "MY_SECRET_KEY" },
    { "Region", "us-east-1" }
};
g4Client.Environments.SetEnvironment("ProductionEnv", parameters, encode: true);

// 3. Retrieve a parameter (decoded)
var apiKey = g4Client.Environments.GetParameter("ProductionEnv", "ApiKey", decode: true);
Console.WriteLine($"Decoded API Key: {apiKey}");

// 4. Invoke an automation
// (Assume we have a G4AutomationModel with tasks defined)
var automationModel = new G4AutomationModel { /* ... configuration ... */ };
var results = g4Client.Automation.Invoke(automationModel);

foreach (var result in results)
{
    Console.WriteLine($"Automation Group: {result.Key}, Status: {result.Value.Status}");
}
```

> **Note:** This snippet is purely illustrative and may be modified to fit real-world scenarios.

For the full walkthrough, with a compiled `G4AutomationModel` sample, the event list, the queue manager and flows, read [Embed G4 in a .NET application](docs/embed-g4-in-dotnet.md).

---

## Contributing

1. **Fork** this repository and clone your fork.
2. Create a **feature branch** (e.g., `feature/awesome-new-feature`).
3. **Commit** your changes and push them to your fork.
4. Open a **Pull Request** against the `main` branch of this repository.

We welcome contributions of all types - bug reports, fixes, new features, documentation updates, etc. Please make sure to follow the project's code style and standards.

---

## License

This project is licensed under the [Apache License 2.0](LICENSE). By contributing to **G4.Api**, you agree that your contributions will be licensed under its Apache 2.0 License.

## Contributing and builds

Build tools, test commands, version numbers, Docker images and development ports are in the [build, test and release guide](../g4-github-actions/docs/build-test-release.md).
