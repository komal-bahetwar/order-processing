# AGENTS.md

This repository is the take-home Order Processing assignment, run through the
AI-SDLC process mounted at `agentic-sdlc/`.

## Read first

- `docs/assignment.md`: the assignment.
- `docs/AGENTIC_SDLC_RUNBOOK.md`: the process runbook. Stages S0 to S8,
  gates G0 to G7, and the paste-ready prompt for every stage and gate.
- `docs/Order_Processing_System_HLD.md`: the high-level design and the
  S3 input context.
- `agentic-sdlc/docs/01-process.md`, `agentic-sdlc/docs/02-artifacts.md`, and
  `agentic-sdlc/docs/05-agent-team.md`: the authoritative process, artifact, and
  agent definitions.

## Rules

- Follow the runbook. Run one stage at a time and stop at each gate.
- Gates are human. Agents draft, execute, and review; the human approves. An
  agent never approves, merges, or deploys.
- Executor and reviewer are separate agents: `sdlc-writer`
  (`deepseek/deepseek-flash`) drafts, then `sdlc-reviewer`
  (`deepseek/deepseek-v4-pro`) critiques the artifact alone in a fresh context.
  Cap the loop at five rounds, then escalate to the human.
- Allocate artifact ids with `agentic-sdlc/scripts/alloc-id`; never hand-assign.
  The SSH remote is blocked by the sandbox, so pass `--local` (single-writer
  repo); never hand-assign ids. Check provenance with
  `agentic-sdlc/scripts/validate --root docs --json`.
- Every artifact carries provenance frontmatter and the approver is always human.
- No implementation starts before its governing spec is approved.

## Build and run

- In this sandbox, set `DOTNET_CLI_HOME="$PWD/.dotnet"` and
  `NUGET_PACKAGES="$PWD/.nuget/packages"` before any `dotnet` command.
- Stack: .NET 10, ASP.NET Core, EF Core 10, Npgsql, PostgreSQL 18, Hangfire.
- Run inside the `opencode-docker` nono profile so Docker and the database are
  reachable.

## Start

Run the Kickoff prompt in the runbook, then the S0 prompt. Stop at G0.
