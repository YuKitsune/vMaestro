---
sidebar_position: 3
---

# Server Deployment

The vMaestro server enables multi-user operation, allowing multiple controllers to collaborate on a single sequence in real-time.

## Overview

The server is an ASP.NET Core application that uses SignalR for real-time communication. It includes a web dashboard for monitoring active sessions.

:::note
Docker images will be available in a future release.
:::

## Standalone Deployment

### Prerequisites

- .NET 10.0 Runtime
- Server binary from the [GitHub releases](https://github.com/YuKitsune/vMaestro/releases)

### Running

```bash
dotnet run --project source/Maestro.Server/Maestro.Server.csproj
```

Or publish and run:

```bash
dotnet publish source/Maestro.Server/Maestro.Server.csproj -c Release -o ./publish
./publish/Maestro.Server
```

## Configuration

Server configuration is provided via environment variables or `appsettings.json`.

### Environment Variables

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` |
| `ASPNETCORE_URLS` | Binding URLs (e.g., `http://+:8080`) |

## Plugin Configuration

Configure the Maestro Plugin to connect to the server by setting the `Server.Uri` in `Maestro.yaml`:

```yaml
Server:
  Uri: https://your-server.example.com/hub
  Partitions:
    - VATSIM
  TimeoutSeconds: 30
```

The URI must point to the `/hub` endpoint.

## Dashboard

The server provides a web dashboard for monitoring active sessions.
Access the dashboard at the server's root URL (e.g., `https://maestro.example.com/`).

The dashboard displays:

- **Sessions List**: All active sessions with their partition and airport
- **Session Details**: For each session:
  - Current runway mode and acceptance rates
  - Next runway mode (if a configuration change is scheduled)
  - Connected controllers with their callsigns and roles
  - All flights in the sequence with their callsign, runway, state, and scheduled times

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
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

The `Upgrade` and `Connection` headers are required for SignalR WebSocket connections.
