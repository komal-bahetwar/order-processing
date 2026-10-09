---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0010
artifact_type: adr
title: "Centralize build and package policy, and inject time into the domain"
status: draft
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "<...>", role: "Architect", date: <YYYY-MM-DD> }
gate: G3
sources: [PRD-0001, NFR-0001, ADR-0001, ADR-0009]
jurisdiction: []
created: 2026-10-10
updated: 2026-10-10
superseded_by: null
---

# Centralize build and package policy, and inject time into the domain

## Context and problem statement

Three maintainability gaps were found. The target framework, nullable policy,
and implicit usings were repeated across seven project files, and package
versions were pinned in each project, so the same policy lived in many places.
The domain read the clock directly (`DateTimeOffset.UtcNow`) inside creation and
transition, so lifecycle tests could only observe relative time. The
architecture tests checked namespace and type dependencies but not the emitted
assembly references, the domain's public surface, or whether the build and
package policies were actually in effect.

The changes must not alter the dependency graph: every existing package version
stays, and no behavior of the running service changes.

## Decision drivers

1. One place to change build and package policy, consistent across projects.
2. Repeatable builds: a selected SDK and a warnings-as-errors policy.
3. Deterministic lifecycle tests without waits or wall-clock flakiness.
4. Boundaries enforced mechanically rather than by convention.
5. No dependency upgrade and no change to the running contract.

## Options considered

### Option 1: Leave the configuration and checks where they are

Cheapest, but the policy stays duplicated, the domain keeps reading a clock,
and the architecture checks stay shallow. Rejected.

### Option 2: Centralize the build and package policy and inject time (chosen)

Add `Directory.Build.props` for the shared compiler settings, `global.json` to
select the SDK with a controlled roll-forward, and `Directory.Packages.props`
for central package management with exact versions. Pass an explicit
`DateTimeOffset` into the domain and inject the framework `TimeProvider` in the
application layer. Strengthen the architecture tests. Exact package versions are
moved, not changed.

### Option 3: Centralize the build with a major dependency refresh

Rejected: the brief and the preservation rules require the existing exact
versions, including the FluentAssertions 7.2.0 pin.

## Decision

Adopt Option 2.

- `Directory.Build.props` sets `TargetFramework`, `Nullable`, `ImplicitUsings`,
  and `TreatWarningsAsErrors` for every project; the per-project duplicates are
  removed.
- `global.json` selects the .NET 10 SDK with `rollForward: latestFeature` from
  `10.0.100`, so any 10.0.x feature band is accepted locally and in the
  container.
- `Directory.Packages.props` enables central package management and holds each
  exact version once; a project no longer declares a `Version` on a
  `PackageReference`.
- The domain takes an explicit `DateTimeOffset` for creation and transitions and
  no longer reads a clock; the application resolves `TimeProvider` from
  dependency injection.
- The architecture test project gains assembly-reference checks, a domain
  public-surface check, a no-public-mutator check, and a check that the build
  and package policies are in effect.

## Consequences

Policy and versions change in one place, and the build now fails on any warning.
Lifecycle behavior is testable with a controlled timestamp. The boundaries are
checked against the compiled output, not only the source. The cost is a small
amount of shared MSBuild configuration and a stricter build; adding a package
now means one central entry. The dependency graph and the public contract are
unchanged.

## Status

Proposed. The artifact frontmatter stays `draft` until the gate ratifies this
decision, at which point the MADR status becomes accepted.
