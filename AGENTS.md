# Repository Guidelines

## Project Structure & Module Organization

This is a .NET 10 Avalonia solution. Core FIFO calculation logic lives in `src/FIFOCalculator.Engine`, while the shared Avalonia app, views, view models, controls, models, and assets live in `src/FIFOCalculator`. Platform hosts are under `src/FIFOCalculator.Desktop`, `src/FIFOCalculator.Android`, `src/FIFOCalculator.iOS`, and `src/FIFOCalculator.Browser`; note that the current `FIFOCalculator.sln` includes the Desktop and Android hosts, the core app, the engine, tests, and NUKE build project. Tests live in `tests`.

## Build, Test, and Development Commands

- `dotnet restore FIFOCalculator.sln`: restore solution packages and workloads.
- `dotnet build FIFOCalculator.sln`: compile the projects included in the solution.
- `dotnet test tests/TestProject1.csproj`: run the xUnit test suite.
- `dotnet run --project src/FIFOCalculator.Desktop/FIFOCalculator.Desktop.csproj`: launch the desktop Avalonia app locally.
- `./build.sh PackDebian`: run the default NUKE packaging path for Debian output.
- `./build.sh PackWindows`, `./build.sh PackAndroid`, or `./build.sh Publish`: build platform packages through NUKE.

## Coding Style & Naming Conventions

Use C# with nullable reference types enabled. Follow the existing four-space C# indentation and file-scoped namespaces. Prefer clear domain names such as `BalanceCalculator`, `FifoStore`, `EntryViewModel`, and `FiscalYearView`. Keep Avalonia XAML paired with matching `.axaml.cs` code-behind files when needed. The build project has `.editorconfig` rules favoring unqualified member access and expression-bodied members where readable.

## Testing Guidelines

Tests use xUnit with FluentAssertions and CSharpFunctionalExtensions assertions. Name test classes after the unit or behavior under test, for example `BalanceCalculatorTests` or `AvailableYearsViewModelTests`. Prefer behavior-oriented test method names such as `Balance_from_store_operations`. Add regression tests for FIFO accounting rules, fiscal-year filtering, and view model observable behavior when changing those areas.

## Commit & Pull Request Guidelines

Recent history uses conventional commits such as `feat:`, `fix:`, `refactor:`, and `chore:`; use the same format and add `!` for breaking changes. Pull requests should describe the user-visible change, list validation commands run, link relevant issues, and include screenshots or short recordings for UI changes.

## Agent-Specific Instructions

When inspecting Zafiro internals, use the local source instead of guessing from package metadata: `/mnt/fast/Repos/Zafiro` for core code and `/mnt/fast/Repos/Zafiro.Avalonia` for UI controls, panels, styles, and mixins.
