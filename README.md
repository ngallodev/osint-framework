# OSINT Framework

A comprehensive open-source intelligence gathering and analysis platform built with .NET 8, React 18 + TypeScript, Tailwind CSS, MariaDB, and pluggable tooling for SpiderFoot, Sherlock, theHarvester, and Ollama-backed AI insights.
in progress...
but currently set up to send questions or data to a local llm running on an ollama instance.
---

## ⚡ Quick Start

### Local Development (Docker Compose)
```bash
# Clone the repo and enter it
git clone https://github.com/yourusername/osint-framework.git
cd osint-framework

# Bootstrap certificates, environment, and docker-compose stack
bash doc/dev_scripts/bootstrap_dev.sh

# Tail backend logs
docker compose logs -f backend

# Stop everything
docker compose down
```

### Local Development (Manual)
```bash
# Backend
 cd backend/OsintBackend
 dotnet restore
 cp appsettings.json appsettings.local.json   # gitignored
 # edit appsettings.local.json with your connection string + Ollama settings
 dotnet watch run

# Frontend
 cd frontend
 npm install
 npm run dev

 # Optional: run static type checks
 npm run typecheck
```

### Configuration Pattern
```
appsettings.json  -> checked-in defaults
appsettings.local.json -> local secrets (gitignored)
Environment variables -> highest priority overrides
```
Refer to **doc/SETUP.md** for detailed configuration instructions and environment variable names.

---

## 📚 Documentation Map
- **doc/SETUP.md** – step-by-step onboarding + local overrides
- **doc/CLAUDE.md** – distributed architecture guidance and deployment scenarios
- **doc/dataflow.mmd** – high-level dataflow diagram (Mermaid)
- **examples/docker-compose.ollama.remote.yaml** – sample compose file for hosting a dedicated Ollama node

---

## 🐳 Docker Services

| Service | Purpose | Ports | Notes |
| --- | --- | --- | --- |
| `database` | MariaDB 10.11 | 3306 | Seeds from `./database/init` |
| `backend` | ASP.NET Core GraphQL API | 5000 (HTTP), 5001 (HTTPS) | Requires Auth0/JWT or API key; mounts `certificates/dev-https.pfx` |
| `frontend` | React UI (Vite dev / Nginx prod) | 3000 | Uses `REACT_APP_API_URL=https://backend:5001/graphql` |
| `spiderfoot` | Official SpiderFoot image | 5001 | Initial setup runs unauthenticated |
| `tooling` | Kali-based CLI toolbox | — | Bundles Sherlock, theHarvester, Python env |

Rebuild the tooling container after modifying `tooling/Dockerfile`:
```bash
docker-compose build tooling
docker-compose up -d tooling
```

Need a standalone Ollama host? Start from `examples/docker-compose.ollama.remote.yaml` on your AI server and point `Ollama__BaseUrl` at its IP/port. See **doc/SETUP.md** for detailed Ollama configuration instructions.

- Both backend and frontend containers serve HTTPS using the locally generated dev certificate (`certificates/dev-https.pfx/.crt/.key`). Trust this cert in your OS/browser to avoid warnings when hitting `https://localhost:5001` (API) or `https://localhost:3000` (UI).

---

## 🔐 Authentication (Auth0)

Both the API and SPA now require Auth0 sign-in (or an API key for automation). To get a development tenant working:

1. **Create a Single-Page Application** in Auth0 (e.g., `osint-framework`).
2. **Configure URLs**
   - Allowed Callback URLs:  
     `https://localhost:3000/auth-callback.html`  
     `https://10.0.0.146:3000/auth-callback.html` (or your LAN IP)  
   - Allowed Logout URLs: the same origins but without `/auth-callback.html`.  
   - Allowed Web Origins: the same HTTPS origins so silent token refresh works.
3. **Set environment variables**
   - Backend (`appsettings.local.json` or env vars):
     ```json
     "Auth": {
       "Auth0": {
         "Enabled": true,
         "Domain": "your-tenant.us.auth0.com",
         "Audience": "https://osint-api"
       },
       "ApiKeys": [ "local-dev-key" ]
     }
     ```
   - Frontend (`frontend/.env.local` or `.env` for compose):
     ```
     VITE_AUTH0_DOMAIN=your-tenant.us.auth0.com
     VITE_AUTH0_CLIENT_ID=xxxxxxxxxxxxxxxxxxxx
     VITE_AUTH0_AUDIENCE=https://osint-api
     ```
4. **Create a dev user** inside Auth0’s “Username-Password-Authentication” database so engineers can sign in immediately. (Invite real users later or enable social providers as needed.)
5. **Restart** the stack (`docker compose up --build`) and browse to `https://localhost:3000`. You should see the login prompt and, after authenticating, the main UI.

> Need to bypass Auth0 for offline testing? Set `Auth:Auth0:Enabled=false` and use the development issuer/signing key in `appsettings.local.json`, but Auth0 should be the default for all shared environments.

---

## 🔗 GraphQL API
- Endpoint (HTTPS): `https://localhost:5001/graphql`
- Endpoint (HTTP fallback): `http://localhost:5000/graphql`
- Playground: enabled when `ASPNETCORE_ENVIRONMENT=Development`
- Auth: required. Supply `Authorization: Bearer <Auth0 access token>` or `X-API-Key: <key>` for service automation.

### Featured Operations
- `createInvestigation`, `updateInvestigation`, `deleteInvestigation`
- `ingestResult`, `bulkIngestResults`, `deleteResult`
- `analyzeInvestigation`, `generateInferences`, `getOllamaStatus`
- `ollamaHealth` (connectivity + latency check)
- Queries: `investigations`, `investigationById`, `results`, `resultsByInvestigationId`

#### Create an Investigation
```graphql
mutation CreateInvestigation {
  createInvestigation(input: {
    target: "example.com"
    investigationType: "domain"
    requestedBy: "analyst@example.com"
  }) {
    success
    error
    data {
      id
      target
      status
    }
  }
}
```

#### Ingest a Result
```graphql
mutation IngestResult {
  ingestResult(input: {
    investigationId: 1
    toolName: "SpiderFoot"
    dataType: "DomainInfo"
    rawData: "{\"ip\":\"1.2.3.4\"}"
    summary: "Resolved domain"
    confidenceScore: "0.9"
  }) {
    success
    error
    data {
      id
      toolName
      dataType
      collectedAt
    }
  }
}
```

#### AI Analysis via Ollama (synchronous legacy path)
```graphql
mutation AnalyzeInvestigation {
  analyzeInvestigation(investigationId: 1) {
    success
    analysis
    error
    generatedAt
  }
}
```

#### Queue-Based AI Workflow (recommended)
Enqueue a background job and poll for completion:

```graphql
mutation QueueAiJob {
  queueAiJob(input: { investigationId: 1, jobType: "analysis", model: "llama2" }) {
    success
    error
    data { id status createdAt }
  }
}
```

```graphql
query GetAiJob {
  getAiJob(jobId: 42) {
    id
    status
    result
    error
    createdAt
    completedAt
  }
}
```

The backend runs `AiJobBackgroundService`, which dequeues jobs from the database and calls Ollama asynchronously. Front-end examples of these operations live in `frontend/src/graphql` and `frontend/src/hooks`.

`appsettings.json` exposes an `AiJobQueue` section (`MaxAttempts`, `RetryBackoffSeconds`) that controls retry behaviour for transient Ollama failures. See **doc/SETUP.md** for configuration details.

#### Ollama Health Check
```graphql
query OllamaHealth {
  ollamaHealth {
    baseUrl
    isAvailable
    statusMessage
    latencyMilliseconds
    models
    checkedAt
  }
}
```
Use this query to validate remote connectivity directly from the backend container.

---

## 🤖 Ollama Integration
- Configure `Ollama__BaseUrl`, `Ollama__DefaultModel`, `Ollama__ServiceType` (local/remote) in `appsettings.*` or environment variables.
- Backend services (`OllamaService`, `RemoteOllamaService`) share `IOllamaService` and use an HttpClient that reads timeout + base URL from configuration.
- Docker Compose defaults to `http://ollama:11434`; adjust when targeting remote hosts.
- See **doc/SETUP.md** for detailed Ollama configuration and remote setup instructions.

---

## 🧪 Testing & Validation
- Backend: `dotnet build`, `dotnet test` (add tests under `backend/OsintBackend.Tests` as they are created).
- Frontend: `npm run test` (when test suites land) and React Testing Library.
- GraphQL: use Playground or `curl` to validate new schema additions.
- Docker: `docker-compose config` to verify YAML; `docker-compose ps` to inspect running services.

---

## 🛣️ Roadmap Snapshot
- External tool integrations (SpiderFoot REST client, Sherlock runner, shared abstractions)
- Frontend UX for tool configuration and results visualization
- Asynchronous orchestration & job tracking
- Enhanced documentation and examples

Contributions are welcome—submit PRs with clear descriptions, include tests when practical, and maintain documentation consistency.

---

## 📄 License
This project is licensed under the MIT License - see the LICENSE file for details.
