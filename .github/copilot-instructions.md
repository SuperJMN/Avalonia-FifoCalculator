# Copilot Instructions — FIFOCalculator

## Build & Test

```bash
# Build the engine (standalone, no UI dependencies)
dotnet build src/FIFOCalculator.Engine/FIFOCalculator.Engine.csproj

# Build the full solution (requires Avalonia workloads)
dotnet build FIFOCalculator.sln

# Run all tests
dotnet test tests/TestProject1.csproj

# Run a single test by name
dotnet test tests/TestProject1.csproj --filter "FullyQualifiedName~SellAtProfit"
```

The Nuke build system (`build/Build.cs`) handles packaging and release. Invoke it via `./build.sh` (Linux) or `build.cmd` (Windows). Key targets: `PackDebian`, `PackWindows`, `PackAndroid`, `Publish`.

## Architecture

This is a cross-platform **FIFO inventory/cost-basis calculator** built with Avalonia UI. It computes capital gains using first-in-first-out accounting.

### Two separate domain models

The codebase has **two parallel domain layers** — this is intentional, not a refactoring oversight:

- **`FIFOCalculator.Engine`** (net8.0) — Pure domain logic. No logging, no DI. Uses `DateTimeOffset` and `Result<T>` from CSharpFunctionalExtensions. Fully tested.
- **`FIFOCalculator/Models/`** (in the UI project) — UI-specific domain with `Maybe<ILogger>` injection and Spanish-language Serilog logging. Uses `DateTime`. Has different `Balance` property names (`SoldValue`/`RemainingValue` vs Engine's `GainOrLoss`/`RemainingInventoryValue`). Not directly tested.

When modifying FIFO calculation logic, check both layers for consistency.

### FIFO algorithm

`FifoStore` uses a `LinkedList<Order>` as a FIFO queue. Buys append to the back; sells consume from the front (oldest-first), partially splitting orders with record `with` expressions. `BalanceCalculator` orchestrates entries through the store, filtering by date range, and accumulating gain/loss.

### UI layer

- **Framework**: Avalonia 11 + ReactiveUI + DynamicData
- **Reactive-first**: All data flows through `IObservable<T>` pipelines — `SourceList<T>.Connect().Transform().Sort().Bind()` chains. No imperative state management.
- **View resolution**: Convention-based `ViewLocator` maps `*ViewModel` → `*View` by name.
- **Composition root**: Manual DI in `Views/CompositionRoot.cs` — no DI container.
- **Platform heads**: Desktop, Android, iOS, Browser (WASM) — all thin bootstrappers referencing the shared UI project.

## Key Conventions

- **Railway-oriented error handling**: Use `CSharpFunctionalExtensions.Result<T>` for operations that can fail. Avoid throwing exceptions for business logic errors. The UI extends this with Zafiro's `.Successes()` and `.HandleErrorsWith()` on `ReactiveCommand`.
- **Immutable records**: Domain types (`Entry`, `Order`, `Balance`) are C# records. Use `with` expressions for modifications.
- **`[Reactive]` attribute** (Fody): Use on ViewModel properties instead of manual `RaiseAndSetIfChanged`.
- **`ObservableAsPropertyHelper<T>`** + `.ToProperty()`: For derived/computed values in ViewModels.
- **`ReactiveValidationObject`**: ViewModels with user input extend this and declare rules via `this.ValidationRule()`.
- **Zafiro toolkit**: Provides `IFilePicker`, `INotificationService`, and the `.Connect()` extension for wiring App → View → ViewModel.
- **Nullable enabled**: All projects use `<Nullable>enable</Nullable>`.

## Test Conventions

- **xUnit** + **FluentAssertions** + **CSharpFunctionalExtensions.FluentAssertions**.
- Tests cover only the Engine project.
- Use `.Should().SucceedWith(value)` to assert both `Result.IsSuccess` and the unwrapped value.
- Use FluentAssertions date helpers for readability: `25.March(2022)`, `1.January(2023)`.
- Global usings in `tests/Usings.cs`: `Xunit` and `FluentAssertions` are available without explicit `using` statements.

## MCP Servers

### Avalonia DevTools

Use the **Avalonia DevTools MCP** to inspect and preview XAML files, attach to a running app, take screenshots, inspect the visual/logical tree, and manipulate properties at runtime.

**Setup**: The project uses `AvaloniaUI.DiagnosticsSupport` with `.WithDeveloperTools()` already configured in `src/FIFOCalculator.Desktop/Program.cs`.

**Usage**:
- `attach-to-file` — Preview a single AXAML file without running the full app.
- `attach-to-app` — Connect to a running Avalonia application for live inspection.
