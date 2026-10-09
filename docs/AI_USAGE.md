# AI usage log

The assignment asks how AI was used across the build, what went wrong, and how
we corrected it. This log answers that directly, stage by stage. It is written
as we go, not reconstructed at the end, so the prompts and the review findings
are the ones that actually happened.

## How we used AI

We ran the build through the mounted AI-SDLC process (`agentic-sdlc/`), which
separates the two jobs AI does for us:

- An **executor** drafts. Here that is the `sdlc-writer` persona on
  `deepseek/deepseek-flash`.
- An **independent reviewer** critiques. Here that is the `sdlc-reviewer`
  persona on `deepseek/deepseek-v4-pro`, a different model, in a fresh context
  that never sees the executor's reasoning.

The reviewer returns `APPROVED` or a numbered issues list. We loop at most five
rounds, then a human decides. Gates stay human: every artifact names a person in
its approver field, and the human records an approve or reject with rationale.

Two mechanical guards keep the output honest:

- Every artifact carries provenance frontmatter, allocated by
  `agentic-sdlc/scripts/alloc-id` and checked by `agentic-sdlc/scripts/validate`.
- The reviewer sees only the artifact and its quality bar, which is what stops
  the loop from rubber-stamping the author's own story.

The rest of the repository records the same thing from the other direction:
gate decisions live in `docs/AGENTIC_SDLC_RUNBOOK.md` section 5, and design
decisions will live as ADRs under `docs/decisions/`.

## People and models

| Role | Model | Notes |
|---|---|---|
| Executor (`sdlc-writer`) | deepseek/deepseek-flash | Drafts artifacts and code |
| Reviewer (`sdlc-reviewer`) | deepseek/deepseek-v4-pro | Independent critique, edit denied |
| Human (all gates) | Komal Bahetwar | Product and engineering hats; owns every approve or reject |

## Code-assistant use

Beyond the process personas, the candidate used editor and chat assistants
(Cursor-style editing and ChatGPT) for scaffolding, phrasing, and review of
code and tests. Those uses are logged in the same shape below from S4 onward,
where the code is actually written, and named explicitly so the walkthrough can
tell process-agent use apart from ad hoc assistant use.

## Log

### Kickoff and S0, Intake and Triage

- **Prompt**: the runbook Kickoff prompt, then the S0 stage prompt
  (`docs/AGENTIC_SDLC_RUNBOOK.md`).
- **What the AI produced**: `BRIEF-0001` (A0.1) and `TRIAGE-0001` (A0.2),
  problem-first, with no technology named.
- **Issues found**: the reviewer blocked the first draft because FR-6 and the
  sizing reasoning named a "scheduled process" and a "fixed cadence". That is a
  mechanism in an S0 artifact, which the quality bar forbids. Four minors came
  with it: `work_item` was a free label, the brief's origin was unrecorded, one
  open question had an unnamed owner, and the size was hedged as "S/M" while
  also calling it tier 2.
- **How we corrected it**: the executor rewrote FR-6 as an outcome (a PENDING
  order reaches PROCESSING automatically within about 5 minutes, no manual
  step), removed the mechanism wording from the sizing, recorded the origin in
  prose, gave every open question a named owner and a due point, and stated the
  size once as Medium. We allocated a real work item, `TKT-0001`, so the
  frontmatter id is not a label. Round 2 was approved.
- **Human decisions**: approved G0. Decided the four open questions are
  answered at S1, not S0, and recorded that deferral.

### S1, Requirements and Definition

- **Prompt**: the runbook S1 stage prompt.
- **What the AI produced**: `PRD-0001` (A1.1), `NFR-0001` (A1.4), and `OQ-0001`
  (open questions).
- **Issues found**: the reviewer approved on round 1 and flagged five minors.
  The PRD called the delivery surface "the API" and the NFR named "page size",
  both borderline mechanism names. One open question was owned by a role, not a
  person. The observability NFR had no number. The transition timing was hedged
  as "about five minutes" in the PRD but stated as "within 5 minutes" in the
  NFR.
- **How we corrected it**: reworded to mechanism-free phrasing ("the
  programmatic interface", "at most 20 results by default and at most 100 on
  request"), named the owner, quantified observability (one log line and one
  counter per transition, health signals within 200 ms), and reconciled the
  timing to "within about 5 minutes" in both artifacts.
- **Human decisions**: approved G1. Resolved the four upstream questions:
  fulfilment and operations drive the forward status changes, the lifecycle is
  forward-only with terminal states, cancellation is whole-order only, and the
  launch volume and list bound are set as an NFR budget. Two new questions
  (duplicate product lines, money precision) stay open with owners and due
  dates.

### S2, Planning and Estimation

- **Prompt**: the runbook S2 stage prompt.
- **What the AI produced**: eight INVEST stories with Given/When/Then
  acceptance criteria (`PRD-0001-S01` to `S08`), the task breakdown
  (`PLAN-0002`), the estimation record (`PLAN-0003`), the release plan
  (`PLAN-0001`), and the risk register (`RISK-0001`).
- **Issues found**: the reviewer blocked round 1 on three majors. The story
  dependencies formed two cycles (for example S07 depended on S04 while S04
  depended on S06). Two requirements had no delivery home at all: the
  observability budget and the list result bound were in the NFR spec but in no
  story and no task, so the G5 tests would have had nothing to run against. Two
  minors came with them. Round 2 found one more major (two list-bound
  acceptance criteria cited FR-5 instead of the NFR they verify) and a minor
  (a risk mitigation claimed S08 delivered an alert it does not).
- **How we corrected it**: fixed the dependency direction to an acyclic order,
  added a dedicated observability story (S08) and list-bound acceptance
  criteria and tasks, relabeled the two mis-cited criteria to NFR-0001, and
  reworded the risk mitigation to attribute the telemetry to S08 and defer
  alerting to the operations artifacts. The estimate rose from 11.5 to 14.5
  days once the two orphaned requirements got stories, and the plan states that
  reconciliation plainly.
- **Human decisions**: approved G2, and accepted the estimate movement and the
  way the plan separates the agent-executed build days from the human budget.

### S2 delta, HLD NFR closure and future phases

- **Prompt**: a human-directed controlled delta between S2 and S3, not a runbook
  stage prompt. The human noticed the approved baseline did not cover every
  non-functional requirement the design context lists, and asked for the gaps
  closed now plus a document for deferred extensions.
- **What the AI produced**: three new non-functional budget rows in `NFR-0001`
  (horizontal scalability, interface contract and error model, operability), a
  correlation identifier added to the observability budget, three new stories
  (`PRD-0001-S09` to `S11`) with tasks, updates to the plan and estimate, and
  `docs/FUTURE_PHASES.md` covering every functional and non-functional
  requirement with phase 2 and phase 3 extensions, including item-level order
  status.
- **Issues found**: the reviewer blocked twice. First, the correlation
  identifier had an acceptance criterion but no delivery task, which is exactly
  the silent gap the process exists to catch. Then the release plan said 25.0
  days while the estimate said 26.0, and a residual "stateless" mechanism leak
  survived in one story note and one task. A third round approved.
- **How we corrected it**: added a correlation task and test assertions,
  reconciled the totals, and removed the mechanism wording.
- **Human decisions**: directed the delta and approved it; chose to keep the
  heavier extensions (item-level status, sharding, versioning, blue/green
  deploys) as future phases rather than current scope.

### S3, Design

- **Prompt**: the runbook S3 stage prompt.
- **What the AI produced**: seven ADRs (`ADR-0001` to `ADR-0007`), three design
  documents (`DOC-0001` C4 views, `DOC-0002` data model and migration,
  `DOC-0003` sequences and state), an OpenAPI 3.1 contract
  (`openapi/order-processing.yaml`), and a test strategy (`TP-0001`).
- **Issues found**: the reviewer blocked round 1 on one major: the contract
  rejected a list request over 100 as a validation error while the story said
  such a request clamps to 100, so the same input had two different outcomes.
  Five minors followed, the notable ones being a missing correlation identifier
  in the required error fields, an unrecorded divergence from the design
  context on the status-update path, and a money-precision rule the contract
  did not state. Two ADRs also lacked the NFR delta in their sources.
- **How we corrected it**: made the contract clamp consistently with the story
  and the design doc and added a test that pins it, required the correlation
  identifier on errors, recorded the automatic-only PENDING to PROCESSING
  decision as a rejected option with a test, reconciled the ADR status
  vocabulary, made the money rounding rule consistent across the contract and
  the design, and added the missing sources.
- **Human decisions**: the two remaining open questions from S1 were closed by
  the design (money precision in ADR-0004, duplicate product lines in ADR-0007),
  and the design context's admin status-override was set aside for a future
  phase. G3 is the human's to record.

### S4, Implementation

- **Prompt**: the runbook S4 stage prompt, applied to the stories as one
  vertical slice rather than one prompt per story.
- **What the AI produced**: the layered .NET 10 solution (domain, application,
  infrastructure, api), the EF Core migration, the Hangfire recurring job, the
  unit and integration test suites, the Docker Compose setup, and the README.
- **Issues found**: three build and runtime issues, then a code-review round.
  Hangfire pulled a vulnerable transitive `Newtonsoft.Json 11.0.1`. An EF Core
  patch conflict broke the build (Npgsql pinned `Relational 10.0.4` while
  `Design` resolved to `10.0.12`). The first migration ran and failed because EF
  generated PascalCase columns while the design's check constraints reference
  snake_case columns. The independent code review then found a real blocker and
  four majors: the list endpoint did not match the contract (it used
  `page`/`pageSize` and a wrapper while the contract uses `limit` and a bare
  array), the single error shape was not produced for unexpected or
  model-binding failures, a single concurrency conflict aborted the rest of the
  automatic-move batch, the observability budget was unimplemented, and the
  tests fell short of the test strategy.
- **How we corrected it**: pinned `Newtonsoft.Json` to `13.0.4` and EF Core
  `Relational` to `10.0.12`; mapped every column to snake_case so the schema
  matches the design; aligned the list endpoint to the contract; wrote one error
  shape for every failure including `500`; reworked the automatic move to fetch
  ids, process one order at a time, and clear the change tracker on a conflict
  so the batch continues; added a per-transition structured log line and counter
  and a correlation id on log lines; and added the missing tests. Round two was
  approved.
- **Human decisions**: chose to fix the blocker and majors before the gate and
  to record the remaining minor findings as debt. At the gate the human also
  asked whether clean architecture was enforced or merely assumed, so an
  architecture test project was added that proves the dependency direction and
  the ORM confinement (5 tests). The build and 45 tests are green.

### S5, Verification and Quality

- **Prompt**: the runbook S5 stage prompt, grounded in the actual test run.
- **What the AI produced**: `TP-0002` (test plan), `DOC-0005` (test report),
  `DOC-0006` (security evidence), and `DOC-0007` (UAT sign-off draft).
- **Issues found**: the reviewer blocked round 1 on two majors. The test plan
  claimed the observability story and budget were covered by integration tests
  when no test actually asserts them, and the test report declared green while
  four performance and availability budgets were unmeasured and no waiver
  existed. Five minors followed, including a wrong approver role on the security
  evidence and a defect-attribution error.
- **How we corrected it**: reconciled the observability coverage to
  "implemented, assertions owed", reframed the gate status as green for the
  automated functional and architecture evidence with a pending waiver for the
  four unmeasured budgets, added the owed assertion to the debt list, fixed the
  defect attribution, changed the security approver to the Security Lead, added
  the missing source, and aligned the UAT statuses with the evidence. Round two
  was approved.
- **Human decisions**: the UAT signature, the waiver for the performance and
  availability budgets, and the G5 decision are the human's to record.

### S6, Release and Deployment

- **Prompt**: the runbook S6 stage prompt, with deployment scoped to local
  Docker Compose.
- **What the AI produced**: `DOC-0008` (change record), `DOC-0009` (deployment
  manifest), `DOC-0010` (rollback plan), and `DOC-0011` (release notes).
- **Issues found**: the deployment check itself surfaced two packaging
  problems. The `postgres:18-alpine` image rejects the old data directory mount
  and failed to start until the volume moved to `/var/lib/postgresql`, and the
  ASP.NET runtime image lacked `libgssapi_krb5`, which produced a startup error.
  The reviewer found four minors: a mis-cited no-threat-model decision, a
  missing client-communications element, incomplete traceability sources, and a
  rollback plan that did not name who exercised it.
- **How we corrected it**: moved the volume, added the native library to the
  runtime image, and re-verified the full stack (`docker compose up --build`
  brought both containers up, the API answered readiness with 200, a created
  order returned a total of 450, and the list endpoint returned it). Fixed the
  four minors.
- **Human decisions**: the G6 approval is the human's to record. The
  deployment scope is stated plainly as the local Compose environment.

### S7 and S8, Operate and Review

- **Prompt**: the runbook S7 continuous prompt and the S8 stage prompt.
- **What the AI produced**: `DOC-0012` (SLO/SLI definitions), `DOC-0013`
  (alert rules), `DOC-0014` (operational runbooks), and `DOC-0015` (post-launch
  review), `DOC-0016` (telemetry report), `DOC-0017` (debt register delta).
- **Issues found**: the reviewer approved with two minors: the availability
  window was stated two ways and the error budget did not match it, and two
  alert rules pointed at runbooks that did not cover their diagnosis path.
- **How we corrected it**: unified the window to the calendar month and fixed
  the error-budget figure, added a high-latency runbook, and repointed the
  rules. The review itself is honest that there is no production traffic, so
  the post-launch review scores the brief success criteria against the 45-test
  run and the local deployment, not adoption.
- **Human decisions**: the joint PM and EM decision at G7 is the human's.

## Human corrections and directions to AI output

The assignment asks what the AI got wrong and how we corrected it. These are the
changes and additions a human made after reviewing the agent's output, separate
from the reviewer findings above. Each one is a judgement the model did not make
on its own. The table covers the corrections (things the model got wrong) and
the directions (things the human asked for that the model had not produced),
because the assignment grades both.

| Where | What the AI produced | What the human changed or asked for, and why |
|---|---|---|
| S0, brief FR-6 | "A scheduled process" and "a fixed cadence" as the requirement wording. | An outcome ("a pending order reaches processing within about 5 minutes, no manual step"), because naming a mechanism in an S0 artifact is a consequence-free way to smuggle a solution into the problem statement. |
| S1, PRD/NFR | "The API" and "page size" as delivery concepts. | Mechanism-free phrasing ("the programmatic interface", "at most 20 results by default and at most 100 on request"), so the requirements state outcomes and leave the how to design. |
| S2, design context | The design context exposed PENDING to PROCESSING as a manual admin transition. | Automatic only, with the admin override deferred. The approved story defines the manual changes as PROCESSING to SHIPPED and SHIPPED to DELIVERED, so the design was aligned to the story and the divergence recorded. |
| S2 delta, scope and scalability | The baseline covered only some non-functional requirements: it missed the design context's horizontal scalability, interface quality, and operability needs, and left item-level order status and other extensions unaddressed. | A human noticed the gaps and directed a delta that added horizontal scalability, interface contract and error model, and operability to `NFR-0001`, with stories `S09` to `S11`, and recorded every deferred functional and non-functional extension, including item-level order status, in `docs/FUTURE_PHASES.md`. |
| S3, concurrency token | An implicit PostgreSQL row-version token (`xmin`) as the optimistic concurrency token. | An explicit `version` column, incremented on each write. `xmin` is a PostgreSQL system column with no portable equivalent, so it would pin the system to one database. The version column gives the same lost-update protection and works on any relational database. The rejected option and its portability cost are recorded in ADR-0002, and the data model, sequences, tests, and design context were updated to match. |
| S3, overall architecture and style | A layered design that never stated the architecture style or gave a single end-to-end picture of the product. | A human asked for the architecture to be documented and named. The result is ADR-0008, which decides a modular monolith with clear module boundaries and defers microservices and a distributed event-driven split with explicit triggers, and an end-to-end architecture diagram in DOC-0001 that shows the modules, the data store, the scheduled path, and the future elements as dashed. |
| S3, deployment and asynchronous evolution | No statement of how the service deploys or how asynchronous integration would evolve. | A human asked for the deployment strategy and the asynchronous posture to be documented. DOC-0004 now states the local Docker Compose deployment (the service and PostgreSQL 18 in one command, migrations on startup), the trigger-based evolution (containers and an orchestrator, a managed database, blue/green or rolling deploys, backups and disaster recovery, read replicas, multi-region), and the asynchronous path (no broker today; a future transactional outbox to a message bus or queue for domain events), with the deployment section of `docs/FUTURE_PHASES.md` carrying the same. |
| S4, clean architecture enforcement | A layered solution whose dependency direction was held by convention only. | A human asked whether clean architecture was enforced or merely assumed. An architecture test project was added that proves the direction mechanically: the domain depends on no other layer and no ORM, the application depends on neither infrastructure nor the ORM, controllers do not reach into persistence, and the application abstractions are implemented in infrastructure. |
| S6/G7, documentation | A project that ran but whose entry points were thin: the README linked only a couple of files, and there was no clear index to the artifacts. | A human asked whether local run, the AI usage log, and the README links were proper. The README gained a documentation index grouped by stage and a project-layout section, the port-override and connection-string details were added to the run instructions, and this AI usage log was confirmed current. |
| S0, open questions | The brief left four unknowns open with owners and due points. | A human asked how open questions should be handled. The decision was that each is resolved at the stage that owns it (the product and scope ones in the S1 PRD, the technical budgets in the S1 NFR), while the S0 baseline only lists unknowns with owners and due dates, never resolving them early by assumption. |
| All stages, the AI usage log itself | The AI usage log could have been reconstructed at the end of the build. | A human asked when to capture it and directed it be maintained continuously, one entry per stage, so the prompts and the review findings are the ones that actually happened rather than a tidy retrospective. |

## Post-close fixes

After the work item closed at G7, the human ran the container and exercised
Swagger, the health probes, and the Hangfire dashboard. That surfaced three
gaps the gates had not caught, because S4 verified the endpoints through the
test host and the integration tests, not through the packaged Development
container. They are recorded here for honesty rather than folded into a stage.

- **Swagger returned 404 in the container.** Compose did not set
  `ASPNETCORE_ENVIRONMENT`, so the API ran as Production and Swagger, mapped
  only in Development, was absent. Fixed by setting the environment to
  Development in `docker-compose.yml`.
- **The Hangfire dashboard was not reachable at all.** The service configured
  Hangfire and the recurring job but never mapped the dashboard. It is now
  mapped at `/hangfire` in Development, with an allow-all authorization filter
  because Hangfire's default filter permits only loopback and a Docker
  port-forward presents the bridge address.
- **The automatic move never registered.** It used the static
  `RecurringJob.AddOrUpdate`, which Hangfire 1.8 rejects at startup when
  `JobStorage.Current` is not initialized, so no recurring job existed and the
  move would never have run. Switched to the dependency-injected
  `IRecurringJobManager`. The dashboard now shows `process-pending-orders` and
  it runs on schedule.

The README now documents testing the health probes and opening the dashboard.
The three fixes are code and configuration changes, committed after the gate,
not review-loop outcomes.

## Keeping this current

Each stage appends one entry in the shape above: the prompt, what the AI
produced, the issues found, how they were corrected, and which decisions were
the human's own. The provenance frontmatter and the gate-decision log are the
machine-readable half of the same record.
