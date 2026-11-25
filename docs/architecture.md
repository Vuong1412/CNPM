# QOL architecture guide

## Dependency direction

The solution follows an inward dependency flow:

1. `QOL.API` receives HTTP requests, applies authentication/authorization and antiforgery validation, then maps requests to application contracts or endpoint services.
2. `QOL.Application` contains use-case abstractions and DTO-independent contracts.
3. `QOL.Domain` contains entities and business invariants.
4. `QOL.Infrastructure` implements application contracts with Entity Framework Core and SQL Server.

Allowed direction:

```text
API -> Application -> Domain
API -> Infrastructure
Infrastructure -> Application
Infrastructure -> Domain
```

Infrastructure must not be referenced directly from Domain or Application code.

## Backend conventions

- Keep EF Core migrations in `backend/QOL.Infrastructure/Migrations`.
- Keep entity validation in Domain entities and request validation in endpoint records.
- Use application interfaces for reusable workflows such as accounts and location management.
- Endpoint classes may use `QolDbContext` only for cohesive read/write endpoints that do not yet have a dedicated application service.
- A future endpoint-specific workflow should be extracted into `QOL.Application` with an interface and implemented in `QOL.Infrastructure`.
- Protected write endpoints must validate antiforgery tokens. The API middleware validates every `POST`, `PUT`, `PATCH`, and `DELETE` request under `/api`.
- User ownership checks must use the `ClaimTypes.NameIdentifier` claim from the authentication cookie, never a client-supplied user ID.

## Frontend conventions

- React pages belong under `frontend/user-web/src/pages`.
- Shared route-independent UI components belong under `components`.
- Shared API contracts and the API client belong under `frontend/shared`.
- Use `ProtectedRoute` for authenticated and administrator-only pages.
- All state-changing API calls must use the shared `apiClient` so the antiforgery header is applied consistently.
- Keep server-derived values authoritative; frontend role checks improve UX but do not replace backend authorization.

## Subscription workflow

1. A user creates a pending order for an active plan.
2. The system prevents duplicate pending orders for the same user and plan.
3. An administrator verifies or rejects the order.
4. Verification creates a `UserSubscription` with the plan duration.
5. Active audio access requires a subscription whose start time has arrived and whose end time is in the future.

Audio media keeps its `/media/audio/{fileName}` URL, but the API streams it only when its audio and location are active and the requesting user has a current subscription. Administrators may preview hidden audio. WAV duration is derived from its RIFF chunks; MP3, OGG, and M4A use the submitted duration with a 1-hour maximum because the current implementation does not parse those formats.

Audio deletion preserves listening history: records with history are hidden, while records without history and their files are deleted. File replacement writes and validates a new file before updating the database, then removes the previous file. The admin orphan-cleanup endpoint supports a dry-run query flag and only scans supported audio extensions in the configured storage directory.

Payment providers and external emails must be isolated behind application interfaces before production integration.

## Testing strategy

- Domain tests verify entity invariants.
- API tests should use an isolated database or test server and verify authentication, authorization, ownership and validation.
- Frontend tests should cover route guards and primary user flows.
- Docker Compose smoke tests should verify health, migrations, login and protected endpoints.
