## What and why

<!-- What this PR changes, and why. -->

## Checklist

- [ ] `dotnet test src/App.slnx` passes locally, and `npm test` in each changed package
- [ ] Is the migration backward compatible? The previous revision of the API keeps serving on the migrated database until the new one is ready, or there is no migration
- [ ] The API, `.Contracts` and integration events only gained fields, or the breaking change is a new version
