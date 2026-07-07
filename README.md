# DataPortal

DataPortal is a web application for managing, importing, and exploring structured research or operational datasets. It provides a central place for users to upload data, organize datasets, review imported content, and launch analysis workflows from a browser.

This public README intentionally provides a high-level explanation only. It does not include production server details, internal paths, credentials, deployment procedures, infrastructure configuration, or security-sensitive operational information.

## What DataPortal Does

DataPortal helps teams work with datasets through a browser-based interface. The platform is designed around a few core goals:

- Centralize access to datasets in one application.
- Support controlled data imports into organized database schemas.
- Provide authenticated access to application features.
- Keep analytics workflows connected to the data portal experience.
- Preserve a clear separation between core application data and imported dataset tables.

## Main Features

### Dataset Management

Users can import datasets into the portal, review the resulting database-backed data, and remove datasets when they are no longer needed. Imported datasets are organized separately from the application's core tables so that user-managed data does not modify protected application structures.

### Authentication and Authorization

DataPortal is designed to use authenticated access. Application permissions are enforced by the portal so that users interact only with the features and data they are allowed to access.

### Analytics Integration

The platform can integrate with notebook-based analytics tools so users can move from managed datasets to exploratory analysis workflows. This keeps analysis close to the data while preserving application-level access controls.

### Logging and Monitoring

The application records operational and security-relevant events so administrators can diagnose issues and review suspicious activity through the configured production logging system.

## Security Principles

DataPortal is intended to follow these security principles:

- Do not store passwords, signing keys, or service credentials in source control.
- Use environment-specific configuration for deployment settings.
- Apply least-privilege access to databases and supporting services.
- Keep imported dataset structures separate from core application tables.
- Route access to supporting services through controlled application entry points.

## Configuration

Runtime configuration is environment-specific and should be supplied by administrators through approved deployment configuration mechanisms. Public documentation should not contain real connection strings, passwords, tokens, signing keys, server names, internal filesystem paths, or service credentials.

## Development Overview

The repository contains the DataPortal application source code. Developers should use standard .NET tooling to restore dependencies, build the project, and run tests according to the environment they are working in.

Typical development activities include:

- Updating application features.
- Maintaining dataset import behavior.
- Managing database migrations.
- Improving authentication and authorization flows.
- Enhancing analytics integration points.
- Reviewing logs and diagnostics during local development.

## Deployment

Deployment details depend on the target environment and should be maintained in private operational documentation. Public documentation should describe the application at a conceptual level only and should not expose infrastructure topology, internal hostnames, deployment scripts, firewall rules, private service ports, or credential-handling instructions.

## For Administrators

Administrators should keep private runbooks for production operations, including deployment steps, infrastructure details, backup procedures, monitoring workflows, and secret rotation. Those runbooks should be stored in an access-controlled location outside this public repository.

## For Contributors

When contributing to DataPortal:

1. Do not commit secrets or environment-specific production configuration.
2. Keep public documentation safe for broad distribution.
3. Document user-facing behavior clearly.
4. Keep operational runbooks separate from public project documentation.
5. Follow the project's existing code style and review process.
