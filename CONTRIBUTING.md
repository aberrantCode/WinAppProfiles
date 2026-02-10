# Contributing

## Branching
1. Create feature branches from `dev` using `feature/<topic>`.
2. Open PRs into `dev`.
3. Promote `dev` into `main` for releases only.

## Commits
Use Conventional Commits:
- `feat: add profile apply summary panel`
- `fix: handle missing executable path gracefully`
- `test: add repository integration coverage`

## Required Checks
- Build: `dotnet build WinAppProfiles.sln`
- Test: `dotnet test WinAppProfiles.sln`
- UI changes include screenshots in PR.
