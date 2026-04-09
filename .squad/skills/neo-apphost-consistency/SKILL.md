name neo-apphost-consistency
description >
  Verify AppHost is the source of truth: services exist in AppHost, dependencies
  are explicit, and service discovery patterns are consistent.
domain architecture
confidence high
source manual

# Neo AppHost Consistency

## When to use

Use this skill when:

- Adding a new service, worker, or container
- Adding a new dependency (DB, cache, identity provider)
- Changing service names, endpoints, or references
- Introducing a new HTTP client or API consumer

## What to check

### 1) Topology is declared in AppHost

- Every runnable service appears in AppHost.
- Names are stable and intentional (service discovery hostnames).

### 2) Dependencies are explicit

- Consumers declare dependencies via references.
- No hidden coupling through environment variables or hardcoded addresses.

### 3) Discovery-friendly clients

- HTTP clients use logical service identifiers, not raw URLs.
- No ports are embedded in code for inter-service communication.

### 4) Consistency across hosts

- Web, Hybrid, and other hosts do not implement their own topology logic.
- Shared RCL stays host-agnostic (interfaces only).

## Output format

- A list of dependencies per project
- Missing references (if any)
- Recommended AppHost changes
- Risk notes if names or discovery patterns will break deployments