---
sidebar_position: 3
---

# Server Deployment

The vMaestro server enables multi-user operation, allowing multiple controllers to collaborate on a single sequence in real-time.

## Overview

The server is an ASP.NET Core application that uses SignalR for real-time communication. It can be deployed:

- Using Docker (recommended)
- As a standalone application

## Docker Deployment

The recommended approach is to use Docker Compose.

### Prerequisites

- Docker and Docker Compose installed
- Access to the vMaestro container image

### Running with Docker Compose

1. Create a `docker-compose.yaml` file:

```yaml
services:
  maestro-server:
    image: ghcr.io/yukitsune/vmaestro-server:latest
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
```

2. Start the server:

```bash
docker-compose up -d
```

The server will be available at `http://localhost:5000`.

### With Seq Logging

For production deployments, Seq provides log aggregation:

```yaml
services:
  maestro-server:
    image: ghcr.io/yukitsune/vmaestro-server:latest
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341

  seq:
    image: datalust/seq:latest
    ports:
      - "5341:80"
    environment:
      - ACCEPT_EULA=Y
```

Access Seq at `http://localhost:5341`.

## Standalone Deployment

### Prerequisites

- .NET 10.0 Runtime

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
