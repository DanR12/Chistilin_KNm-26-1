# OrderFlow

A small order-and-invoicing service: web interface, REST API, SQLite database. It is the product under test in the laboratory works of the course *Software Testing, Verification, and Validation*. This is release **1.2**.

## Run it

Requirements: .NET SDK 10 (`dotnet --version` should print 10.x).

```
ORDERFLOW_VARIANT=<your number>  dotnet run --project src/OrderFlow.Api
```

On Windows PowerShell:

```
$env:ORDERFLOW_VARIANT = "<your number>"; dotnet run --project src/OrderFlow.Api
```

Then open http://localhost:5000. The API description is at http://localhost:5000/swagger. The variant number is printed in the page footer.

The database is the file `orderflow.db` next to the compiled application (`src/OrderFlow.Api/bin/Debug/net10.0/orderflow.db` when started with `dotnet run`). It is created and filled with demo data on first start. To return to the demo data, log in as the manager and use *Reset demo data* on the Log page, or `POST /api/test/reset`.

With Docker instead of the SDK:

```
docker build -t orderflow .
docker run -p 5000:5000 -e ORDERFLOW_VARIANT=<your number> orderflow
```

## Test it

```
dotnet test
```

runs every test project in the solution: the five smoke tests in `tests/OrderFlow.Smoke` and the domain tests in `tests/OrderFlow.Domain.Tests`. To run one project only:

```
dotnet test tests/OrderFlow.Domain.Tests
```

`.github/workflows/ci.yml` is the GitHub Actions workflow that runs the same three commands (restore, build, test) on every push and pull request.

## Accounts

| Username | Password | Role |
|---|---|---|
| manager | manager | manager |
| alice | alice | customer (VIP) |
| bob | bob | customer |
| carol | carol | customer |

## What is in the box

- `src/OrderFlow.Api` — web pages and API (ASP.NET Core)
- `src/OrderFlow.Application`, `src/OrderFlow.Domain`, `src/OrderFlow.Infrastructure` — the layers behind it
- `tests/OrderFlow.Smoke` — five smoke tests through the web host
- `tests/OrderFlow.Domain.Tests` — the unit tests of the domain (release 1.2: five starter tests)
- `.github/workflows/ci.yml` — the CI workflow
- `kit/PRODUCT.md` — the product description
- `kit/requests.http` — a request notebook for the API
- `kit/templates/` — investigation note, bug report, significance table, PROOF report, dashboard; and the Lab 1 templates

## New in 1.2

No functional changes. The solution now contains `tests/OrderFlow.Domain.Tests`, a unit-test project over `OrderFlow.Domain` with five starter tests, and the CI workflow is complete. Release 1.1 added the Outbox page and `GET /api/outbox`, the request log with error ids and `GET /api/log`, `POST /api/test/reset` and the *Reset demo data* button, the Support inbox (manager) and five more demo orders. See `kit/PRODUCT.md`.
