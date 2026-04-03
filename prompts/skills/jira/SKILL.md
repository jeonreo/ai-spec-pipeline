---
name: jira
description: "Convert the detailed spec into a human-friendly Jira ticket. Keep the title and description concise, readable, and PM-friendly while preserving actionable acceptance criteria."
---

Transform the spec into a Jira-ready JSON object.

Core intent:

- The spec is the detailed source of truth for agents and engineers.
- The Jira output is a human-facing handoff artifact for PMs and collaborators.
- Jira should be easy to scan quickly in the UI.
- Do not dump the full spec into the Jira description.

Rules:

- Output JSON only. No markdown fences, commentary, or extra text.
- `summary` is the Jira title. It must be short, natural, and easy for a human to scan.
- `summary` should describe the outcome, not the implementation mechanism.
- `description` should read like a clean PM handoff, not like an internal prompt dump.
- Keep each description field concise. Prefer 1-3 short paragraphs or tight bullets worth of content.
- Put deep technical detail in the spec, not in Jira.
- Acceptance criteria should be concrete, testable, and phrased as outcomes.
- If something is unresolved, put it in `Open Questions / Dependencies`, not in the main title.

Title guidance for `summary`:

- Good: "Add saved filter presets to the member search page"
- Good: "Improve failed payment retry visibility for admins"
- Avoid: "Implement CQRS endpoint and FE component changes for..."
- Avoid: "Spec for..."

Description guidance:

- Background: why this matters
- Goal: expected outcome
- Scope: what is included
- Out Of Scope: what is explicitly excluded
- User Impact: who benefits or what changes
- Risks / Dependencies: blockers, coordination points, assumptions
- Open Questions: unresolved items only when necessary
