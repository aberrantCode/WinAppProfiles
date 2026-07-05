---
# Base — required
feature: <human-readable name, e.g. "State Control">
slug: <kebab-case; for a sub-feature, the filename minus the topic prefix>
status: drafted            # drafted | approved | deployed | discontinued | superseded
priority: p2               # p1 | p2 | p3
area: <one of: profile-management | state-control | discovery | status-monitoring | persistence | user-interface | startup-integration | settings>

# Lifecycle dates
date_drafted: YYYY-MM-DD
date_approved: null        # required once status >= approved
date_last_revised: YYYY-MM-DD

# Attribution
author: <name/handle>
reviewer: null             # required once status >= approved (can be self)

# Optional — supersession chain (only when relevant)
# supersedes: docs/features/<old-spec>.md
# superseded_by: docs/features/<new-spec>.md

# Optional — related docs
# related: [docs/features/<sibling>.md, docs/plans/<plan>.md]
---

## Overview

One or two paragraphs: what this capability is and why it exists. For an
**umbrella** topic that owns multiple sub-surfaces, replace this simpler
shape with the umbrella body shape from the
[README](README.md#umbrella-body-shape-102) (`Use cases` →
`Cross-cutting constraints` → `Cross-cutting risks` → `Out of Scope` →
`Sub-surfaces`).

## Capabilities

- [ ] <testable capability the surface provides>
- [ ] <...>

## Requirements

**Must** (required for the feature to be considered complete):
- The system must <...>

**Should** (expected but not blocking):
- The system should <...>

**May** (optional enhancement):
- The system may <...>

## Acceptance Criteria

- [ ] AC1: Given <context>, when <action>, then <observable outcome>
- [ ] AC2: <...>

## Out of Scope

- <what this feature explicitly does NOT do>

## Notes

- <implementation notes, key types, design references>
