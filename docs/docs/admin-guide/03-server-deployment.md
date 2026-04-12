---
sidebar_position: 3
---

# Server Deployment

The vMaestro server enables multi-user operation, allowing multiple controllers to collaborate on a single sequence in real-time.

## Overview

The server is a relay server. It does not calculate or persist the sequence. All sequencing logic runs in the plugin on the master controller's machine. The server's role is to coordinate between connected clients:

- The first controller to connect to a session becomes the **master**. Their plugin calculates the sequence and broadcasts updates to all peers.
- Other controllers connect as **slaves**. Their plugins receive sequence state from the master and relay any actions (runway changes, manual delays, etc.) back to the master for processing.
- When the master disconnects, the server promotes the next eligible connected controller to master.

Session state is held in memory only for the duration of an active session. It is not calculated on the server and not written to disk. The only data persisted to disk are logs.

The server is an ASP.NET Core application that uses SignalR for real-time communication. It includes a web dashboard for monitoring active sessions.

A single server instance supports multiple **environments**, enabling the same airport to have multiple isolated sessions. Each environment maintains its own state and does not affect others. Common uses include separating live VATSIM operations from training sessions.

## Docker Deployment

Docker is the recommended deployment method. Images are published to the GitHub Container Registry on each release.

```
ghcr.io/yukitsune/vmaestro:latest
```

```yaml
services:
  maestro:
    image: ghcr.io/yukitsune/vmaestro:latest
    ports:
      - "8080:8080"
    volumes:
      - ./data:/app/data
    restart: unless-stopped
```

### Persistent Data

The server writes logs to `/app/data/logs/` inside the container. Mount a volume at `/app/data` to retain logs across container restarts. No sequence state is written to disk.

## DigitalOcean App Platform

You can deploy the server to DigitalOcean App Platform using the provided template.

[![Deploy to DO](https://www.deploytodo.com/do-btn-blue.svg)](https://cloud.digitalocean.com/apps/new?repo=https://github.com/yukitsune/vmaestro/tree/main)

The deploy template is available in the repository at `.do/deploy.template.yaml`. You can use it as a starting point, or deploy directly using the DigitalOcean CLI:

```bash
doctl apps create --spec .do/deploy.template.yaml
```

## Standalone Deployment

If Docker is not available, the server can be run directly using the .NET runtime.

### Prerequisites

- .NET 10.0 Runtime
- Server binary from the [GitHub releases](https://github.com/YuKitsune/vMaestro/releases)

### Running

```bash
dotnet publish source/Maestro.Server/Maestro.Server.csproj -c Release -o ./publish
./publish/Maestro.Server
```

Set the `DATA_PATH` environment variable to control where logs are written (default: `/app/data`):

```bash
DATA_PATH=/var/maestro ./publish/Maestro.Server
```

## Configuration

Server configuration is provided via environment variables or `appsettings.json`.

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` |
| `ASPNETCORE_URLS` | Binding URLs (e.g. `http://+:8080`) |
| `DATA_PATH` | Directory for logs (default: `/app/data`) |

## Plugin Configuration

Configure the plugin to point to the server using `Server.Uri` in `Maestro.yaml`. Define one or more environments under `Server.Environments` -- each environment is an isolated session group clients can connect to.

```yaml
Server:
  Uri: https://maestro.example.com/hub
  Environments:
    - VATSIM
    - Training
  TimeoutSeconds: 30
```

When connecting, controllers select which environment to join from a dropdown in the connection window. Controllers in different environments do not share session state.

See [Plugin Configuration](02-plugin-configuration.md#server-configuration) for the full reference.

## Dashboard

The server provides a web dashboard for monitoring active sessions. Access it at the server's root URL (e.g. `https://maestro.example.com/`).

The dashboard displays:

- **Sessions**: All active sessions by environment and airport
- **Session details**: For each session:
  - Current runway mode and acceptance rates
  - Next runway mode (if a configuration change is scheduled)
  - Connected controllers with callsigns and roles
  - Flights in the sequence with callsign, runway, state, and scheduled times

## Reverse Proxy

For production, place the server behind a reverse proxy (nginx, Caddy, etc.) with TLS termination.

Example nginx configuration:

```nginx
server {
    listen 443 ssl http2;
    server_name maestro.example.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

The `Upgrade` and `Connection` headers are required for SignalR WebSocket connections.
