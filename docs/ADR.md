# Architecture Decision Records

## ADR-001: Configuration File Strategy

**Date:** 2025-07-19  
**Status:** Accepted  

### Context
Need to store both template configuration and real user credentials securely.

### Decision
Use two-file configuration approach:
- `appsettings.json`: Template with placeholder values (committed to git)
- `appsettings.local.json`: Real credentials (gitignored)

### Rationale
- Security: Real credentials never committed to version control
- Team collaboration: Shared template, personal overrides
- Industry standard: Common pattern in .NET applications
- Bootstrap simplicity: No circular dependency on database

### Alternatives Considered
- Database storage: Rejected due to bootstrap complexity
- Single file: Rejected due to security risks
- Environment variables: Rejected due to complexity for multiple credentials

---

## ADR-002: Progressive Configuration Saving

**Date:** 2025-07-19  
**Status:** Accepted  

### Context
Setup wizard was losing user progress when interrupted during development/testing.

### Decision
Save each configuration section immediately after completion using `SaveConfigurationSectionAsync`.

### Rationale
- Better developer experience during iterative testing
- Fault tolerance for long setup processes
- User confidence - no lost work

### Implementation
- JSON merging to preserve existing sections
- Section-by-section saving (Jira, RepositoryProviders, Workflow, Database)
- Error handling with user feedback

---

## ADR-003: JQL-Based Task Queries

**Date:** 2025-07-19  
**Status:** Accepted  

### Context
Manual project key entry was cumbersome and error-prone.

### Decision
Use JQL (Jira Query Language) queries instead of project-specific filters.

### Rationale
- More flexible: Users can customize queries beyond project scope
- User-centric: Default to `assignee = currentUser()`
- Familiar: JQL is standard for Jira power users
- Future-proof: Supports complex filtering scenarios

### Default Query
```jql
assignee = currentUser() AND status IN ("To Do", "In Progress", "Ready for Development")
```

---

## ADR-004: Provider-Specific UI Labels

**Date:** 2025-07-19  
**Status:** Accepted  

### Context
Generic "Repository Configuration" was confusing to users.

### Decision
Show specific provider names in setup wizard (e.g., "BitBucket Configuration").

### Rationale
- Clarity: Users know exactly which service they're configuring
- Consistency: Matches pattern with "Jira Configuration"
- Extensibility: Easy to add new providers with clear labeling

### Implementation
- Dynamic header based on selected provider
- Maintains generic language for selection step
- Future-ready for multiple provider support
