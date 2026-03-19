---
sidebar_position: 4
---

# API Access

The vMaestro server provides a REST API for integration with external applications. API documentation is provided via Swagger.

## Accessing the API Documentation

Each vMaestro server instance hosts its own API documentation. Access it at:

```
https://your-server.example.com/swagger
```

Replace `your-server.example.com` with your server's address.

## Overview

The API allows external applications to:

- Query the current sequence state
- Retrieve flight information
- Subscribe to real-time updates

## Authentication

Refer to your server's Swagger documentation for authentication requirements specific to your deployment.

## Note on Documentation Hosting

The API documentation is hosted by each vMaestro server instance. This design allows:

- Each deployment to document its specific API version
- Administrators to configure authentication independently
- API changes to be reflected accurately per deployment

For API integration, always reference the Swagger documentation from your target server instance.
