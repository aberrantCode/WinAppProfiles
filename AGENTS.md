# Repository Guidelines

## Project Structure & Module Organization
- `src/WinAppProfiles.UI`: WPF shell, views, view models, and app bootstrap.
- `src/WinAppProfiles.Core`: domain models, interfaces, and profile orchestration logic.
- `src/WinAppProfiles.Infrastructure`: SQLite persistence, discovery, process/service state control, and startup task registration.
- `tests/WinAppProfiles.Unit`: fast logic tests for profile reconciliation and filtering.
- `tests/WinAppProfiles.Integration`: SQLite and repository integration tests.

## Build, Test, and Development Commands
- `dotnet restore`: restore NuGet packages for all projects.
- `dotnet build WinAppProfiles.sln -c Debug`: build all projects.
- `dotnet test WinAppProfiles.sln -c Debug`: run unit and integration tests.
- `dotnet run --project src/WinAppProfiles.UI`: run the desktop app locally.

## Coding Style & Naming Conventions
- Use 4-space indentation and file-scoped namespaces in C#.
- Public types/members: `PascalCase`; locals/params: `camelCase`; private fields: `_camelCase`.
- Keep UI logic in view models; keep platform/process/service operations in infrastructure services.
- Use `nullable enable` and prefer explicit DTOs over anonymous payloads.

## Testing Guidelines
- Frameworks: `xUnit`, `FluentAssertions`, `Moq`.
- Test names: `MethodName_Scenario_ExpectedBehavior`.
- Cover profile apply behavior, failure continuation, identity matching (`ExecutablePath + ProcessName`), and Needs Review filtering.
- Target at least 80% coverage for `Core` and `Infrastructure` projects.

## Commit & Pull Request Guidelines
- Branch flow: `feature/* -> dev -> main`.
- Use Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`).
- PRs to `dev` should include: summary, linked issue, test evidence, and screenshots for UI changes.
- Merge `dev -> main` only after test pass and smoke verification.

## Security & Configuration Tips
- App runs elevated to allow service control; document UAC implications in release notes.
- Keep environment-specific settings in `appsettings.*.json`; never commit secrets.
- Log to local rolling files with structured context for supportability.
