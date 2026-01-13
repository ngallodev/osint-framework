# !/bin/bash
# This script updates the database schema using Entity Framework Core migrations.
# From the host machine, run this script from the osint-framework root directory.
# requirements: .NET SDK installed, dotnet-ef tool installed, and MySQL container running, 
dotnet-ef database update \
  --project backend/OsintBackend \
  --startup-project backend/OsintBackend \
  --connection "Server=127.0.0.1;Port=3307;Database=OsintFramework;User=osintuser;Password=YourPasswordHere"

