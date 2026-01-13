# Development Scripts

This directory contains utility scripts for development and maintenance of the OSINT Framework.

## Available Scripts

### `create_dev_cert.sh`

Generates development HTTPS certificates for Kestrel (.NET backend).

**Usage:**
```bash
./doc/dev_scripts/create_dev_cert.sh [password]
```

**Output:** Creates `./certificates/dev-https.pfx`

**Environment Variables:**
- `DEV_CERT_PASSWORD` - Certificate password (default: `localdevpass`)

---

### `generate_erd.sh`

Automatically generates an Entity Relationship Diagram (ERD) from the Entity Framework model.

**Usage:**
```bash
./doc/dev_scripts/generate_erd.sh
```

**Output:** Creates/updates `./doc/osint-framework-erd.vuerd.json`

**What it does:**
- Parses all C# model files in `backend/OsintBackend/Models/`
- Reads the DbContext configuration from `backend/OsintBackend/Data/OsintDbContext.cs`
- Extracts table definitions, columns, data types, and constraints
- Identifies relationships between entities
- Generates a VUERD (Visual Universal ERD) JSON file

**How to view the ERD:**
1. **VS Code:** Install the "ERD Editor" extension by dineug
2. **Web:** Upload the file to https://www.erdcloud.com/

**When to run:**
- After adding new entity models
- After modifying existing models
- After changing database relationships
- Before creating database migrations (to verify schema)

**Requirements:**
- Python 3
- Access to the backend source code

**Example workflow:**
```bash
# Make changes to your models
vim backend/OsintBackend/Models/MyNewEntity.cs

# Regenerate the ERD
./doc/dev_scripts/generate_erd.sh

# Open in VS Code to visualize
code doc/osint-framework-erd.vuerd.json
```

---

## Notes

- All scripts should be run from the project root directory
- Scripts use `set -euo pipefail` for safer bash execution
- Ensure scripts are executable: `chmod +x doc/dev_scripts/*.sh`
