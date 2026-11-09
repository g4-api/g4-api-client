# Embed G4 in a .NET application

This guide shows how to run G4 inside your own .NET process with the `G4.Api` package. You build an automation in C#, invoke it, listen to its events, and read the response. No Hub is needed.

## Who this is for

You write a .NET application, a test harness or a service, and you want to run G4 automations from code.

If you want to call a running G4 Hub over HTTP instead, read [When to use the client and when to use the Hub](#when-to-use-the-client-and-when-to-use-the-hub).

## What you need

| Need | Detail |
| --- | --- |
| .NET | The .NET 10 SDK. `G4.Api` targets `net10.0`. |
| Package | `dotnet add package G4.Api` |
| Driver | The driver your automation names, for example Chrome and a matching ChromeDriver. |
| Token | A G4 token in `authentication.token` of the automation model. |

## Create a G4Client

`G4Client` is the entry point. Its constructors take any of three optional parts, a `CacheManager`, an `IQueueManager` and an `ILogger`. A part you leave out gets a default.

| Part | Default |
| --- | --- |
| `CacheManager` | `CacheManager.Instance` |
| `IQueueManager` | A new `BasicQueueManager` |
| `ILogger` | `G4Logger.Instance` |

```csharp
using G4.Api;

var client = new G4Client();
```

Source: `src/G4.Api/G4Client.cs`.

## The sub-clients

Each property on `G4Client` is one sub-client.

| Property | Interface | Use it to |
| --- | --- | --- |
| `Automation` | `IAutomationClient` | Invoke an automation and wait for the result. Raises the full set of events. |
| `AutomationAsync` | `IAutomationAsyncClient` | Queue automations and run them one at a time from a queue. |
| `Flows` | `IFlowsClient` | Register, list, invoke and remove flows by namespace and key. |
| `Environments` | `IEnvironmentsClient` | Store and read environment parameters. |
| `Templates` | `ITemplateClient` | Add, list and remove plugin templates. |
| `Integration` | `IIntegrationClient` | Read the plugin cache and documents, list drivers, and sync external repositories and MCP servers. |
| `Credentials` | `ICredentialsClient` | Save and read OAuth credentials. |
| `Logger` | `ILogger` | The logger the client was built with. |

Sources: `src/G4.Api/Abstractions/I*Client.cs`.

## Build a G4AutomationModel in C#

An automation is stages, which hold jobs, which hold rules. The next method builds the same automation as the JSON in [Module 1 of Understand a G4 Automation](../../g4-engine/docs/understand-a-g4-automation/01-what-an-automation-is.md): open a page, check the title, close the browser.

```csharp
using G4.Api;
using G4.Extensions;
using G4.Models;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

private static G4AutomationModel NewAutomation()
{
    // Describe the work as three rules: open a page, assert on the title and close the browser.
    var rules = new List<G4RuleModelBase>
    {
        new ActionRuleModel(pluginName: "OpenUrl") { Argument = "http://example.com" },
        new ActionRuleModel(pluginName: "Assert")
        {
            Argument = "{{$ --Condition:PageTitle --Operator:Match --Expected:Example}}"
        },
        new ActionRuleModel(pluginName: "CloseBrowser")
    };

    // Group the rules into a job, and the job into a stage.
    var job = new G4JobModel
    {
        Reference = new G4JobReferenceModel { Name = "Main job" },
        Rules = rules
    };
    var stage = new G4StageModel
    {
        Reference = new G4StageReferenceModel { Name = "Main stage" },
        Jobs = [job]
    };

    // Assemble the automation with its driver, reference and settings.
    return new G4AutomationModel
    {
        Authentication = new AuthenticationModel { Token = "<your-g4-token>" },
        DriverParameters = new Dictionary<string, object>
        {
            ["driver"] = "ChromeDriver",
            ["driverBinaries"] = "."
        },
        Reference = new G4AutomationReferenceModel
        {
            Id = "minimal-example",
            Name = "Open, assert, close"
        },
        Settings = new G4SettingsModel
        {
            AutomationSettings = new AutomationSettingsModel
            {
                LoadTimeout = 60000,
                SearchTimeout = 15000,
                MaxParallel = 1,
                ReturnStructuredResponse = true
            },
            ExceptionsSettings = new ExceptionsSettingsModel { ReturnExceptions = true }
        },
        Stages = [stage]
    };
}
```

Notes on the model:

- `ActionRuleModel(pluginName)` creates a rule of `$type` `Action`. The other rule types are `ContentRuleModel`, `DataCollectorRuleModel`, `ExtractionRuleModel`, `SwitchRuleModel` and `TransformerRuleModel`.
- `DriverParameters` is a dictionary. The keys match the JSON property names, `driver` and `driverBinaries` here.
- `Stages`, `Jobs` and `Rules` must not be empty, and `DriverParameters` is required.
- The same model reads from JSON. `FlowsClient` decodes a stored flow with `JsonSerializer.Deserialize<G4AutomationModel>` and `PropertyNameCaseInsensitive = true`, so camelCase and PascalCase names both bind.

Sources: `g4-engine/src/G4.Models/AutomationModels.cs`, `PhaseModels.cs`, `PhaseRuleModels.cs`, `ReferenceModels.cs`, `SettingsModels.cs`.

## Invoke an automation and read the response

`Automation.Invoke` runs the automation and blocks until it finishes. It returns a dictionary. Each key is the reference id of one automation, and each value is a `G4AutomationResponseModel`.

The client expands the model with `NewAutomations` before it runs, so the dictionary can hold more than one entry, for example when the automation has a `dataSource`.

```csharp
private static void Invoke(G4Client client, G4AutomationModel automation)
{
    // Subscribe before invoking, because events fire while the call is running.
    client.Automation.RuleInvoked += (_, args) =>
        Console.WriteLine($"{args.Rule.PluginName}: {args.RuleStatus.Status}");
    client.Automation.AutomationInvoked += (_, args) =>
        Console.WriteLine($"Automation done: {args.AutomationReference}");
    client.Automation.OnRuleError += (_, error) =>
        Console.WriteLine($"Rule failed: {error.Exception.Message}");

    // Invoke the automation and read one response per automation reference id.
    var responses = client.Automation.Invoke(automation);
    foreach (var (id, response) in responses)
    {
        Console.WriteLine($"{id}: {response.Sessions.Count} session(s)");
    }
}
```

A `G4AutomationResponseModel` holds `Sessions` (the per-session results), `PerformancePoint` and `DataProvider`. Which parts are filled depends on the `Return*` flags in `settings`.

Sources: `src/G4.Api/Clients/AutomationClient.cs` (`Invoke`), `g4-engine/src/G4.Models/ResponseModels.cs`.

## Events

Subscribe before you call `Invoke`. The events fire while the call is running. To stop a run, use a callback event (see the table) or `StopAutomation`.

| Event | Payload | When it fires |
| --- | --- | --- |
| `AutomationInvoking` | `AutomationEventArgs` | Before an automation is invoked. |
| `AutomationInvoked` | `AutomationEventArgs` | After an automation is invoked. `Response` holds the result. |
| `AutomationStatusChanged` | `AutomationEventArgs` | When the status of an automation changes. |
| `AutomationRequestInitialized` | `AutomationQueueModel` | When an automation request is set up. |
| `AutomationStopped` | `int` | After `StopAutomation` runs. The value is the exit code, `0` for success and `1` for a failure. |
| `StageInvoking`, `StageInvoked`, `StageStatusChanged` | `StageEventArgs` | Around a stage, and when its status changes. |
| `JobInvoking`, `JobInvoked`, `JobStatusChanged` | `JobEventArgs` | Around a job, and when its status changes. |
| `RuleInvoking`, `RuleInvoked`, `RuleStatusChanged` | `RuleEventArgs` | Around a rule, and when its status changes. |
| `OnRuleError` | `(RuleEventArgs EventArguments, Exception Exception)` | When a rule throws. |
| `PluginCreated` | `(PluginBase Plugin, G4RuleModelBase Rule)` | After a plugin instance is created for a rule. |
| `LogCreating`, `LogCreated` | `LogEventArgs` | Before and after a log entry is created. `LogMessage` is a dictionary. |
| `RuleCallback` | `RuleCallbackEventArgs` | After each rule. Carries a `CancellationTokenSource`, the `Client`, the `FlowsClient` and the `Queue`. |
| `AutomationCallback` | `AutomationCallbackEventArgs` | After each automation. Carries the same members as `RuleCallback`, with `AutomationEventArgs`. |

What the main payloads carry:

| Payload | Members |
| --- | --- |
| `AutomationEventArgs` | `Automation`, `AutomationGroup`, `AutomationReference`, `AutomationStatus`, `Response` |
| `StageEventArgs` | `Automation`, `AutomationGroup`, `AutomationReference`, `AutomationStatus`, `Stage`, `StageReference`, `StageStatus` |
| `JobEventArgs` | The stage members, plus `Job`, `JobReference`, `JobStatus` |
| `RuleEventArgs` | The job members, plus `Rule`, `RuleReference`, `RuleStatus`, `Response`, `Driver`, `Exceptions`, `Extractions`, `PluginType`, `SessionParams` |

The two callback events are the only ones built for control. After your handler returns, the client checks the cancellation token and throws if it is cancelled. Call `Cancel()` on `CancellationTokenSource` to end the run from inside a handler.

Stop a run from outside with `Automation.StopAutomation(automationId)`. It returns the same exit code that `AutomationStopped` reports.

Sources: `src/G4.Api/Abstractions/IAutomationClient.cs`, `src/G4.Api/Clients/AutomationClient.cs` (the callback registration in the invoke task and `StopAutomation`), `src/G4.Api/Models.Events/*.cs`, `g4-engine/src/G4.Plugins/Models/*EventArgs.cs`, `g4-engine/src/G4.Models/EventModels.cs`.

## Run from a queue with AutomationAsync and IQueueManager

`AutomationAsync` does not run an automation straight away. It puts the automation on the queue of an `IQueueManager`, and `StartAsync` takes one pending automation and runs it.

```csharp
private static async Task InvokeAsync(G4AutomationModel automation)
{
    // Own the queue manager so it can be inspected.
    var queueManager = new BasicQueueManager();
    queueManager.ModelEnqueued += (_, model) => Console.WriteLine($"Queued: {model.Automation.Reference.Id}");
    var client = new G4Client(queueManager);

    // Queue, then run one pending automation.
    client.AutomationAsync.AddPendingAutomation(automation);
    var responses = await client.AutomationAsync.StartAsync().ConfigureAwait(false);
    Console.WriteLine(responses.Count);
    Console.WriteLine(client.AutomationAsync.Completed.Count);
}
```

How it works:

- `AddPendingAutomation` expands the model with `NewAutomations` and adds each resulting automation to the queue manager as a pending entry.
- `StartAsync` moves one pending entry to active, invokes it and returns a dictionary keyed by the reference id. Call it again for the next entry. It returns an empty dictionary when nothing is pending.
- Finished results are kept in `Completed`, keyed by reference id. `Active` holds the automations that are running.
- `IAutomationAsyncClient` raises only `AutomationRequestInitialized` and `AutomationInvoked`. The other events in its interface are commented out in the source. `AutomationStatusChanged` is declared on the client but nothing raises it.

`IQueueManager` is the queue behind it. Pass your own to the `G4Client` constructor to inspect or replace the queue.

| Member | Purpose |
| --- | --- |
| `Pending`, `Active`, `Errors` | The three queues. |
| `AddPending`, `AddActive`, `AddError` | Add entries. They do nothing while the manager is paused. |
| `GetPending`, `GetActive`, `GetErrors` | Read entries. |
| `UpdateActive` | Replace an active entry. |
| `Pause`, `Resume`, `Reset` | Control the manager. |
| `ModelEnqueuing`, `ModelEnqueued`, `ModelDequeuing`, `ModelDequeued` | Events around the queue. |
| `OnError` | An entry moved to the error queue. |

`BasicQueueManager` is the default. In its source, `ModelDequeuing` is declared but never raised.

Sources: `src/G4.Api/Clients/AutomationAsyncClient.cs`, `src/G4.Api/IQueueManager.cs`, `src/G4.Api/BasicQueueManager.cs`.

## Environments

Environment parameters are stored by name. `encode: true` stores the values as Base64, and `decode: true` reads them back as text.

```csharp
private static void UseEnvironments(G4Client client)
{
    // Store one parameter, Base64-encoded, in the Staging environment.
    var parameters = new Dictionary<string, string> { ["ApiKey"] = "MY_SECRET_KEY" };
    client.Environments.SetEnvironment("Staging", parameters, encode: true);
    // Read the parameter back decoded.
    var apiKey = client.Environments.GetParameter("Staging", "ApiKey", decode: true);
    Console.WriteLine(apiKey);
}
```

A bot reads the same value with the `Get-Parameter` macro. See [Module 5 of Understand a G4 Automation](../../g4-engine/docs/understand-a-g4-automation/05-parameters-and-environments.md).

Source: `src/G4.Api/Abstractions/IEnvironmentsClient.cs`.

## Flows

A flow is a complete automation stored by namespace and key. The `automation` field of the manifest is the automation JSON, Base64-encoded. `AddFlow` returns `204` when saved and `400` when the manifest has no key or the automation does not decode. `Invoke` returns the status code with the responses: `200` on success, `404` when the flow is not cached, `400` when the flow cannot be decoded after its parameters are applied.

```csharp
private static void UseFlows(G4Client client, G4AutomationModel automation)
{
    // Encode the automation as Base64 JSON.
    var json = JsonSerializer.Serialize(automation);
    var manifest = new G4FlowManifestModel
    {
        Key = "OpenExample",
        Namespace = "Samples",
        Automation = json.ConvertToBase64()
    };

    // Register, invoke and remove.
    var statusCode = client.Flows.AddFlow(manifest);
    var (invokeStatus, responses) = client.Flows.Invoke("Samples", "OpenExample");
    Console.WriteLine($"{statusCode} {invokeStatus} {responses?.Count}");
    client.Flows.RemoveFlow("Samples", "OpenExample");
}
```

To pass values, give `Invoke` a `G4FlowInvokeModel` with `Parameters`, `Argument`, `DriverParameters`, `Settings` or `Authentication`. The flow keeps what it already sets, and your values fill the gaps.

Sources: `src/G4.Api/Clients/FlowsClient.cs`, `g4-engine/src/G4.Models/AutomationModels.cs` (`G4FlowInvokeModel`).

## When to use the client and when to use the Hub

| Choose | When |
| --- | --- |
| `G4.Api` in your process | You own the host. You want typed models, events in C# and no network hop. The browser or driver starts on the machine that runs your code. |
| The Hub REST API | Another language or tool calls G4, many callers share one engine, or you want Workers and grids. See the [REST API guide](../../g4-services/docs/rest-api/README.md). |
| The Hub MCP endpoint | An AI agent drives G4. See the [MCP path](../../g4-services/docs/mcp/README.md). |

Both routes use the same engine. The Hub hosts it behind HTTP, and `G4.Api` hosts it in your process.

## How the samples were checked

The samples in this guide were compiled against the source project in a scratch console project. They were not run, because a run needs a driver and a token.

Back to the README.
