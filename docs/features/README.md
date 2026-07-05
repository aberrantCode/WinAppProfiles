# Doc Conventions — Features & Plans

> **This file is the authoritative reference** for the filename grammar and
> the frontmatter contract that every doc under `docs/features/` and
> `docs/plans/` must follow. Read it before authoring or revising a spec.

This contract is adapted for **WinAppProfiles** from the AC_OPBTA docs
convention. The *contract* — frontmatter fields, filename grammar, body
shapes, and the status lifecycle — is preserved verbatim. Only the
project-specific parts (the canonical topic list, examples, and
inapplicable networked-service fields) have been re-derived for this
repo, which ships a single-user Windows desktop application rather than a
homelab fleet.

---

## The taxonomy

Every doc under `docs/features/` and `docs/plans/` belongs to one of the
**canonical topics**. The topic appears as the filename prefix:

| Layer | Pattern | Example |
|---|---|---|
| Feature spec (primary) | `docs/features/<topic>.md` | `docs/features/state-control.md` |
| Feature spec (sub-feature) | `docs/features/<topic>--<descriptor>.md` | `docs/features/profile-management--low-power-wizard.md` |
| Plan | `docs/plans/<topic>--<descriptor>.md` | `docs/plans/profile-management--low-power-wizard.md` |
| Runbook | `docs/runbooks/<topic>--<aspect>.md` (or `docs/RUNBOOK.md` while singular) | `docs/RUNBOOK.md` |

The canonical topics for WinAppProfiles:

`profile-management`, `state-control`, `discovery`, `status-monitoring`,
`persistence`, `user-interface`, `startup-integration`, `settings`.

These eight map onto the app's layered architecture (see the "Architecture
Overview" in the root [`CLAUDE.md`](../../CLAUDE.md)): the Core domain +
orchestration surface (`profile-management`), the two Infrastructure
control/query surfaces (`state-control`, `discovery`, `status-monitoring`),
the SQLite/settings persistence surface (`persistence`), the WPF surface
(`user-interface`), the OS-integration surface (`startup-integration`), and
the settings surface (`settings`).

A topic may carry additional **sub-feature** specs named
`<topic>--<descriptor>.md` when one slice is large enough to warrant its
own surface. For example, both wizards live under `profile-management`:
`profile-management--creation-wizard.md` and
`profile-management--low-power-wizard.md`. The parent relationship is
encoded by the filename grammar itself — `profile-management--low-power-wizard.md`
cannot have any parent except `profile-management.md` — so sub-feature
specs do **not** restate it in frontmatter.

## Filename grammar

- **Topic slug:** kebab-case, drawn from the eight-topic list above.
- **`--` (double dash):** reserved as the topic-from-descriptor separator.
- **Descriptor:** kebab-case, unique within its topic.
- **No dates in filenames.** Authorship and revision dates live in
  frontmatter (`date_drafted`, `date_last_revised`) and `git log`.

## Doc-type semantics

| Type | Purpose | Body shape |
|---|---|---|
| Feature spec | What + why + acceptance criteria for a topic | Primary specs follow the **umbrella shape** (see below) when the topic owns multiple sub-surfaces or tools. Single-surface topics may keep the simpler overview/capabilities/requirements/acceptance shape. One primary spec per topic; optional sub-feature specs for large slices. |
| Plan | Self-contained design + implementation steps for one slice of work | Point-in-time. Design content lives **in** the plan — no separate design files. One plan per slice. `feature_ref:` is required. |
| Runbook | Operational / contributor procedure | Living document. |

---

## Frontmatter contracts

There are two contracts that concern this repo: **feature spec** (§10.1)
and **plan** (§10.3). The shapes below are authoritative.

### Feature spec frontmatter (§10.1)

Use [`TEMPLATE.md`](TEMPLATE.md) when authoring a new spec. The fields:

```yaml
---
# Base — required
feature: <human-readable name, e.g. "State Control">
slug: <kebab-case; matches filename minus topic-prefix when a sub-feature>
status: <drafted | approved | deployed | discontinued | superseded>
priority: <p1 | p2 | p3>
area: <one of the 8 canonical topics; matches `slug` for primary features>

# Lifecycle dates
date_drafted: YYYY-MM-DD             # required — first draft committed
date_approved: YYYY-MM-DD | null     # required once status >= approved
date_last_revised: YYYY-MM-DD        # required — bump on every substantive change

# Attribution
author: <name/handle>                # required — first author of the spec
reviewer: <name/handle> | null       # required once status >= approved (can be self)

# Optional — supersession chain
supersedes: docs/features/<old-spec>.md       # what this spec replaces
superseded_by: docs/features/<new-spec>.md    # required once status = superseded

# Optional — related docs
related: [docs/features/<sibling>.md, docs/plans/<plan>.md]
---
```

**Required:** `feature`, `slug`, `status`, `priority`, `area`,
`date_drafted`, `date_last_revised`, `author`.
**Conditionally required:** `date_approved` and `reviewer` once
`status >= approved`; `superseded_by` once `status = superseded`.

**Sub-feature parent:** encoded by the filename
(`<topic>--<descriptor>.md`), not by frontmatter. There is no
`parent_feature:` field — see "Filename grammar" above.

**Excluded from feature specs:** `depends_on:` — per-slice prerequisites
belong on plans, not features. (This captures the "umbrella ≠ delivery
artifact" distinction: a feature spec describes a capability, a plan
describes the work to deliver it.)

**Pruned from the upstream contract:** the `infra_requirements:` block
(internal_only / auth_required / public_endpoint_allowed) and the
`categories:` owner-taxonomy — both are networked-fleet concepts that do
not apply to a single-user local desktop app. Do not add them here.

### Plan frontmatter (§10.3)

```yaml
---
# Base — required
plan: <human-readable, e.g. "Low Power Wizard">
slug: <kebab-case; matches filename minus topic prefix>
feature_ref: docs/features/<topic>.md       # REQUIRED — the parent feature spec
status: <drafted | in-progress | completed | abandoned | superseded>
priority: <p1 | p2 | p3>

# Lifecycle dates
date_drafted: YYYY-MM-DD
date_approved: YYYY-MM-DD | null
date_last_revised: YYYY-MM-DD

# Attribution
author: <name/handle>
reviewer: <name/handle> | null

# Per-slice prerequisites (belongs HERE, not on the feature spec)
depends_on:
  - <plan-path | external-prereq>

# Optional
failures: 0                                  # plan-runner failure counter
supersedes: docs/plans/<old>.md
superseded_by: docs/plans/<new>.md
related: [<path>, ...]
---
```

**Required:** `plan`, `slug`, `feature_ref`, `status`, `priority`,
`date_drafted`, `date_last_revised`, `author`.

**Status enum semantics:** the plan `status:` field tracks workflow
lifecycle (`in-progress`, `completed`, `abandoned`) — distinct from the
feature-spec `status:` field which tracks capability existence in the
shipped app. Both fields are named `status` for familiarity.

---

## Status lifecycle (§10.5)

Feature spec lifecycle and plan workflow are distinct state machines:

```
Feature spec lifecycle (capability existence in the shipped app):
  drafted ──> approved ──> deployed ──┬──> discontinued  (retired, no successor)
                                      └──> superseded    (replaced; superseded_by set)

Plan workflow (delivery state):
  drafted ──> in-progress ──> completed
              │       │
              v       v
            blocked  abandoned
```

Reverse transitions (e.g. `approved` → `drafted`) are allowed but rare;
flag them in the commit message and bump `date_last_revised`.

---

## Umbrella body shape (§10.2)

When a feature spec covers a topic that hosts **multiple sub-surfaces**
(e.g. `user-interface` covers the Card shell + Tabbed shell + tray + icon
extraction), structure the body as an umbrella with per-surface sections
at the bottom that point at sub-feature specs or plans:

```markdown
# <Feature topic title>

## Use cases
What the user wants to do in this domain (testable, surface-agnostic).

## Cross-cutting constraints / substrate decisions
Decisions every sub-surface inherits unless explicitly overridden.

## Cross-cutting risks
Risks every sub-surface inherits.

## Out of Scope (umbrella-level, not per-surface)
What this feature is NOT trying to do.

## Sub-surfaces

### <Sub-surface Name>
- **slug:** <kebab-case>
- **status:** <drafted | approved | deployed | discontinued>
- **spec / plan:** docs/features/<topic>--<descriptor>.md | docs/plans/<...>.md
- **capability:** <one-line: what it does for this feature>
- **key types:** <the Core/UI types that implement it>

### <Next Sub-surface Name>
...
```

Sub-headings are **guidance, not strict requirements** — features can
extend or omit per their actual surface.

Single-surface topics (e.g. `discovery`, `status-monitoring`) may keep the
simpler body shape used by the seed spec
[`profile-management--low-power-wizard.md`](profile-management--low-power-wizard.md):
`Overview` → `Capabilities` → `Requirements` (Must/Should/May) →
`Acceptance Criteria` → `Out of Scope` → `Notes`. Use whichever shape fits
the surface; do not force an umbrella onto a single-surface topic.

---

## Where things go

| What | Where |
|---|---|
| Feature specs | `docs/features/<topic>.md` |
| Sub-feature specs | `docs/features/<topic>--<descriptor>.md` |
| Implementation plans | `docs/plans/<topic>--<descriptor>.md` |
| Contributor / operational procedure | [`docs/RUNBOOK.md`](../RUNBOOK.md), [`docs/CONTRIB.md`](../CONTRIB.md) |
| Point-in-time solution audit | [`docs/current-solution-analysis.md`](../current-solution-analysis.md) |
| User journeys | [`_project_specs/journeys/`](../../_project_specs/journeys/) |
| Superseded feature drafts | `docs/features/archive/` |

### Templates

| Template | Purpose |
|----------|---------|
| [`TEMPLATE.md`](TEMPLATE.md) | New feature spec (frontmatter per §10.1) |

---

## Supersession

**Feature spec superseded:**

- Set `status: superseded` in frontmatter and add `superseded_by:`
  pointing at the new path.
- Move the file to `docs/features/archive/` (keep the topic prefix).

**Plan superseded:**

- Set `status: abandoned` (or `status: superseded` with `superseded_by:`
  if a direct replacement exists).
- Move to `docs/plans/archive/` if it exists.

---

## Why this contract exists

A feature spec answers **what a capability is, why it exists, and how you
know it's done** — durable across implementation churn. A plan answers
**how the work gets delivered** — point-in-time. Keeping the two contracts
distinct (and keeping `depends_on:` off features) prevents the umbrella
spec from rotting into a delivery checklist. The `date_drafted` /
`date_last_revised` naming makes the lifecycle semantic unambiguous —
avoiding the "is this git-mtime or last-reviewed?" ambiguity of a single
`last_updated` field.
