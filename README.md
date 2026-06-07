# Be Welcome!

Greedy is a simple online dice game. I developed to serve as a sample for the following practices / patters:

* Event Sourcing & CQRS
* Test Driven Development
* Clean Architecture
* Event Modelling

The game is developed as a .Net WebApi and Blazor server apps. You will find:

* The source code in src/ with the application and service layers of the solution
* The tests/ folder covers unit tests, integration tests for the WebApi, and e2e tests for the UI.

# Running locally

Secrets are **not** committed to source. Before running the app or the
docker-compose stack, provide them locally as follows.

## App secrets (.NET user-secrets)

The WebApp reads its Identity database connection string and the JWT signing key
from configuration. In development, set them via [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(stored outside the repo) — run these from `src/WebApp`:

```bash
cd src/WebApp
dotnet user-secrets set "ConnectionStrings:Identity" "Host=localhost;Port=5432;Database=farkle_identity;Username=farkle;Password=farkle_dev_password"
dotnet user-secrets set "Auth:JwtSecret" "<any string of at least 32 characters>"
```

The EventStore connection string falls back to `esdb://localhost:2113?tls=false`
(matching docker-compose), so it only needs a user-secret if you run ESDB elsewhere.

## docker-compose stack

The compose file reads its passwords/connection strings from a git-ignored
`.env` file. Create one from the template:

```bash
cp .env.example .env   # adjust if needed; the defaults work for local dev
docker-compose up -d
```

# Contributing

Feel free to add any issues and or requests features. You can find the work to be done on the project here.
