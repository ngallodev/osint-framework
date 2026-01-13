# OSINT Framework - Local Setup Guide

This guide walks you through setting up the OSINT Framework for local development, including configuring secure local overrides for secrets and credentials.

## Quick Start

### Prerequisites

- .NET 8.0+
- Node.js 18+
- Docker & Docker Compose
- MySQL/MariaDB (or use Docker version)

### 1. Clone Repository

```bash
git clone <repository-url>
cd osint-framework
```

### 2. One-Line Bootstrap (Recommended)

```bash
bash doc/dev_scripts/bootstrap_dev.sh
```

This script will:

1. Copy `.env.example` to `.env` (if missing).
2. Generate a development HTTPS certificate (`certificates/dev-https.pfx`) using `dotnet dev-certs https`.
3. Start the full docker-compose stack.

After the script completes:

- Backend API (HTTPS): https://localhost:5001/graphql  
- Backend API (HTTP fallback): http://localhost:5000/graphql  
- Frontend UI: https://localhost:3000  
- SpiderFoot: http://localhost:5001 (from spiderfoot container)

> **Trusting the dev certificate**: The generated certificate lives under `certificates/dev-https.pfx` with password `localdevpass` (configurable via `.env`). Add it to your OS/browser trust store if you see certificate warnings.

### 3. Manual Backend Setup (Optional)

```bash
cd backend/OsintBackend

# Create local configuration
cp appsettings.json appsettings.local.json

# Edit with your local values
nano appsettings.local.json
# Update ConnectionStrings with your database password

# Restore dependencies
dotnet restore

# Run migrations
export PATH="$HOME/.dotnet/tools:$PATH"
dotnet-ef database update

# Start backend
dotnet run
```

### 3. Frontend Setup

```bash
cd frontend

# Create local environment configuration
cp .env .env.local

# Edit if needed (usually works as-is)
nano .env.local

# Install dependencies
npm install

# Start dev server
npm run dev
# Frontend will be available at http://localhost:5173

# Tailwind CSS utilities are available across the app via `src/styles/index.css`.

# Optional: run the TypeScript checker without emitting output
# npm run typecheck
```

## Configuration Pattern Explained

The framework uses a **safe defaults + local overrides** pattern:

```
Checked into Git          NOT in Git (gitignored)
┌─────────────────┐      ┌──────────────────┐
│ appsettings.json│ ──→ │appsettings.local │ → Final Config
│ (safe defaults) │      │ .json (secrets)  │
└─────────────────┘      └──────────────────┘
```

### For Each Config File

| File | Location | In Git? | Contains | Purpose |
|------|----------|---------|----------|---------|
| appsettings.json | /backend/OsintBackend | ✅ YES | Safe defaults | Template & documentation |
| appsettings.local.json | /backend/OsintBackend | ❌ NO | Secrets, passwords | Local overrides with real credentials |
| .env | /frontend | ✅ YES | Safe defaults | Frontend configuration template |
| .env.local | /frontend | ❌ NO | Custom values | Frontend local overrides |
| docker-compose.yml | / | ✅ YES | Service definitions | Base service configuration |
| docker-compose.local.yml | / | ❌ NO | Local values | Docker compose overrides |

## Configuration Details

### Backend Configuration (appsettings.local.json)

After copying `appsettings.json` to `appsettings.local.json`, update:

#### Database Connection

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=OsintFramework;User=osintuser;Password=YOUR_LOCAL_PASSWORD;"
  }
}
```

#### Ollama AI Model

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ServiceType": "local",
    "DefaultModel": "llama2",
    "TimeoutSeconds": 300
  }
}
```

#### Using a Remote Ollama Host

If you run Ollama on a separate machine (e.g., GPU server), update the configuration accordingly:

```json
{
  "Ollama": {
    "BaseUrl": "http://192.168.1.100:11434",
    "ServiceType": "remote",
    "DefaultModel": "llama2",
    "TimeoutSeconds": 300
  }
}
```

1. Deploy the remote host using **examples/docker-compose.ollama.remote.yaml** on the target machine.  
2. Ensure port `11434` is reachable from the backend machine.  
3. Set firewall rules or security groups to restrict access to trusted callers.  
4. Verify connectivity before starting the backend:

```bash
curl http://192.168.1.100:11434/api/tags
```

Tip: you can also override at runtime with an environment variable:

```bash
export Ollama__BaseUrl=http://192.168.1.100:11434
export Ollama__ServiceType=remote
```

### Development HTTPS Certificate -> for localhost

- Docker-compose mounts the contents of `./certificates` into both backend and frontend containers so they can terminate HTTPS.
- The bootstrap script runs `doc/dev_scripts/create_dev_cert.sh` to generate `dev-https.pfx` (for Kestrel) plus `dev-https.crt`/`dev-https.key` (for nginx) using `dotnet dev-certs` + `openssl`.
- The certificate password comes from `DEV_CERT_PASSWORD` in your `.env` (default `localdevpass`).
- To regenerate manually with a custom password:

```bash
bash doc/dev_scripts/create_dev_cert.sh "your-password"
```

Update `.env` with the same password before restarting the stack. Trust the exported cert in your OS/browser to avoid warnings when visiting `https://localhost:5001` or `https://localhost:3000`.

### To setup a cert for your servere's local ip if not working on localhost
- Follow the instructions above to set up https certificates, then:
- Modify dev_scripts/dev-ip.conf, set the ip aaddress (10.x.x.x etc).
- Run the two scripts in dev_scripts/gen_ip_certs.sh
- Modify .env and docker-compose.yml and nginx.conf to point to the newly generated cert, key file, and pfx.
- Do a docker compose down && docker compose up -d --build frontend backend
- Trust the new cert in your browser.

### AI Job Queue Background Worker
- The backend hosts `AiJobBackgroundService`, which polls the `AiJobs` table and invokes Ollama asynchronously.
- Use the `queueAiJob` GraphQL mutation to enqueue work, then poll `getAiJob`/`getAiJobsForInvestigation` for status updates.
- Jobs transition through statuses: `Queued → Running → Succeeded/Failed/Cancelled`.
- All job data is stored in the MariaDB database; ensure migrations run (`dotnet ef database update`).
- Deployments with multiple backend instances should leverage the same database to avoid duplicate processing.
- Configure retry behaviour via `AiJobQueue` in `appsettings.*`:

```json
"AiJobQueue": {
  "MaxAttempts": 3,
  "RetryBackoffSeconds": 5
}
```

- `MaxAttempts` controls how many times a job is retried before being marked as failed.
- `RetryBackoffSeconds` introduces a delay before the background worker retries a failed job.

#### External Tools

```json
{
  "Tools": {
    "SpiderFoot": {
      "Url": "http://localhost:5001",
      "TimeoutSeconds": 600
    },
    "Sherlock": {
      "SherlockPath": "/opt/sherlock/sherlock.py",
      "PythonPath": "python3",
      "TimeoutSeconds": 60
    }
  }
}
```

### Frontend Configuration (.env.local)

After copying `.env` to `.env.local`, update:

```bash
# GraphQL API endpoint (change if backend is on different port)
VITE_REACT_APP_API_URL=http://localhost:5000/graphql

# Optional: Enable debug logging
VITE_DEBUG_MODE=false
```

### Docker Compose Overrides (docker-compose.local.yml)

Example docker-compose.local.yml:

```yaml
version: '3.8'

services:
  database:
    environment:
      MYSQL_ROOT_PASSWORD: mypassword123
      MYSQL_PASSWORD: mypassword123

  backend:
    environment:
      ConnectionStrings__DefaultConnection: Server=database;Database=OsintFramework;User=osintuser;Password=mypassword123;
      Ollama__BaseUrl: http://ollama:11434
```

## Database Setup

### Using Docker

```bash
docker-compose -f docker-compose.yml -f docker-compose.local.yml up database

# Apply migrations (from within container)
docker-compose -f docker-compose.yml -f docker-compose.local.yml exec backend \
  dotnet-ef database update
```

### Local Database

```bash
# Install MySQL/MariaDB locally, then update appsettings.local.json

# Create database
mysql -u root -p
> CREATE DATABASE OsintFramework;
> CREATE USER 'osintuser'@'localhost' IDENTIFIED BY 'password123';
> GRANT ALL PRIVILEGES ON OsintFramework.* TO 'osintuser'@'localhost';

# Apply migrations
cd backend/OsintBackend
dotnet-ef database update
```

## Running the Application

### Full Stack with Docker

```bash
# Start all services
docker-compose -f docker-compose.yml -f docker-compose.local.yml up

# Stop services
docker-compose -f docker-compose.yml -f docker-compose.local.yml down
```

### Backend Only (Local Development)

```bash
cd backend/OsintBackend
dotnet run

# Backend GraphQL endpoint: http://localhost:5000/graphql
# Backend Swagger: http://localhost:5000/swagger/index.html
```

### Frontend Only (with existing backend)

```bash
cd frontend
npm run dev

# Frontend: http://localhost:5173
```

## Common Issues

### "appsettings.local.json not found"

This is OK! The app works with just appsettings.json. To use local overrides:

```bash
cp appsettings.json appsettings.local.json
# Edit appsettings.local.json with your settings
```

### Database connection refused

- Check database is running: `docker ps` or `mysql -u root -p`
- Check password in appsettings.local.json matches database
- Check database server address (localhost vs. database container name)

### GraphQL endpoint returns 404

- Make sure backend is running on correct port (5000)
- Check VITE_REACT_APP_API_URL in .env.local
- Make sure GraphQL is mapped: `http://localhost:5000/graphql`

### Docker container exits immediately

```bash
# Check logs
docker-compose -f docker-compose.yml -f docker-compose.local.yml logs backend

# Rebuild
docker-compose -f docker-compose.yml -f docker-compose.local.yml build --no-cache
```

### "CHANGE_ME" in appsettings

This is intentional! It means you need to create and edit appsettings.local.json with real values.

## Next Steps

1. **Explore the codebase** - See `/backend` and `/frontend` directories
2. **Read CLAUDE.md** - Architecture and deployment guide
3. **Review the examples folder** - Sample configurations for different deployment scenarios

## Security Notes

⚠️ **IMPORTANT**

- Never commit appsettings.local.json, .env.local, or docker-compose.local.yml
- Check .gitignore includes all .local patterns
- Don't hardcode real credentials in base files
- Use environment variables in production
- Rotate passwords regularly

## Getting Help

1. Check the documentation files in `/doc`
2. Review error logs: `docker-compose logs <service>`
3. Test database connection: `mysql -u user -h host -p`
4. Check network connectivity between services

## IDE Setup

### Visual Studio Code

```json
{
  ".debug": "Install C# extension and launch.json",
  "tasks": {
    "build": "dotnet build",
    "run": "dotnet run",
    "test": "dotnet test"
  }
}
```

### JetBrains Rider

- Import project directly
- Configurations → Add Configuration → .NET Launch Settings Profile
- Select OsintBackend profile

### Visual Studio

- Open `backend/OsintBackend.sln`
- Set OsintBackend as startup project
- Press F5 to run

## Development Workflow

1. Create feature branch: `git checkout -b feature/my-feature`
2. Make changes (avoid editing example/base config files)
3. Test locally with .local overrides
4. Commit changes (don't commit .local files)
5. Push and create pull request

Remember: Local overrides are never committed, keeping secrets safe! 🔒
