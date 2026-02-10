APM_STANDARDS {

## Code Conventions
- Follow `.editorconfig` for all formatting and styling.
- Use single-file FastEndpoints per endpoint feature (endpoint + DTOs + validator/mapper if needed in one file).
- Keep endpoints within their existing module projects; group by feature folders.

## Quality Requirements
- Preserve existing behavior and request/response contracts (strict refactor only).
- Do not change existing route paths/URLs.
- Add TODO markers wherever authorization was previously commented out to preserve current behavior while flagging follow-up work.

## Process Standards
- Add temporary route-parity tests during migration and remove them after user confirmation of parity.
- Run `dotnet format` for `apps/api/LibraFoto.slnx` and resolve all warnings/errors.
- Update `docs/api/*` to reflect FastEndpoints + vertical slice structure without changing route listings.

## Tool/Technology Standards
- Use the latest available FastEndpoints version compatible with .NET 10.

} //APM_STANDARDS
