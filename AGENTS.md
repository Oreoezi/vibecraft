# VibeCraft agent contract

## Work boundaries

- Start from a numbered issue. State the issue, bounded outcome, and owned paths before editing.
- Use one branch and one PR per bounded issue. Do not mix cleanup, refactors, or another issue's work into it.
- Claim disjoint paths. If another change owns a needed path, coordinate or stop; do not overwrite it.
- Inspect the relevant code, tests, decisions, and current diff before editing. Preserve all user work and unrelated changes.

## Core compatibility rules

- Use the named scalar domains in `docs/implementation/COMPATIBILITY_SURFACE.md`; do not invent substitute scalar vocabulary or cast between domains because their widths match.
- A CLR type or in-memory struct is never a persistence or wire format. Define explicit fields, widths, ordering, bounds, versioning, and codecs. Never serialize CLR layout, object graphs, reflection names, or raw memory.
- G1 core is Godot-free and has no Godot, SQLite, or GameNetworkingSockets dependency. Use ephemeral fixtures only.
- Do not create a user world or compatibility promise before its required gate is greenlit.

## Evidence and handoff

- Validate the changed contract with focused tests/property tests and deterministic fixtures. Record commands, results, and any unrun validation in the PR.
- Make benchmark claims only against a named fixture with its hardware, workload, duration, and measurement method. Otherwise call the result an observation or hypothesis.
- Before handoff, inspect `git diff` and `git status`; report changed paths, validation evidence, risks, and follow-up issues. Do not revert, stage, delete, or rewrite user work.
- Close only when the issue acceptance criteria and validation evidence are present, the owned-path boundary is clean, and the PR links the issue. Otherwise hand off with the exact blocker and next action.
