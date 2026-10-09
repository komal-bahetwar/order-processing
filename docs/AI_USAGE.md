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

## Human corrections to AI output

The assignment asks what the AI got wrong and how we corrected it. These are the
changes a human made after reviewing the agent's output, separate from the
reviewer findings above. Each one is a judgement the model did not make on its
own.

| Where | What the AI proposed | What the human changed it to, and why |
|---|---|---|
| S3, ADR-0002 | An implicit PostgreSQL row-version token (`xmin`) as the optimistic concurrency token. | An explicit `version` column, incremented on each write. `xmin` is a PostgreSQL system column with no portable equivalent, so it would pin the system to one database. The version column gives the same lost-update protection and works on any relational database. The rejected option and its portability cost are recorded in ADR-0002, and the data model, sequences, tests, and design context were updated to match. |
| S0, brief FR-6 | A "scheduled process" and a "fixed cadence" as the requirement wording. | An outcome ("a pending order reaches processing within about 5 minutes, no manual step"), because naming a mechanism in an S0 artifact is a consequence-free way to smuggle a solution into the problem statement. |
| S1, PRD/NFR | "The API" and "page size" as delivery concepts. | Mechanism-free phrasing ("the programmatic interface", "at most 20 results by default and at most 100 on request"), so the requirements state outcomes and leave the how to design. |
| S2, design context | The design context exposed PENDING to PROCESSING as a manual admin transition. | Automatic only, with the admin override deferred. The approved story defines the manual changes as PROCESSING to SHIPPED and SHIPPED to DELIVERED, so the design was aligned to the story and the divergence recorded. |
| S2 delta, scope | The baseline did not cover every non-functional requirement in the design context. | A human noticed the gaps and directed a delta to close them (scalability, interface quality, operability) and to record deferred extensions, including item-level order status, in `docs/FUTURE_PHASES.md`. |

## Keeping this current

Each stage appends one entry in the shape above: the prompt, what the AI
produced, the issues found, how they were corrected, and which decisions were
the human's own. The provenance frontmatter and the gate-decision log are the
machine-readable half of the same record.
