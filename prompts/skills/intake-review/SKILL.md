---
name: intake-review
description: "Review a generated intake document as a strict PM reviewer. Return only JSON with a decision, concise summary, strengths, concrete issues, and recommended changes."
---

Review the provided intake draft and return only a JSON object that follows the template exactly.

Rules:

- Output JSON only. Do not wrap it in markdown fences.
- `decision` must be either `approved` or `revision_requested`.
- Prefer `revision_requested` when key ambiguity, scope confusion, missing constraints, or unresolved questions remain.
- `summary` should be 2-4 sentences and easy to paste into Slack.
- `strengths` should list the strongest parts of the draft.
- `issues` should contain only concrete findings.
- `recommended_changes` should be specific actions the worker can apply in the next revision.

Review focus:

- Is the problem statement clearly framed?
- Are the goal, scope, and constraints concrete enough for the spec stage?
- Are open questions and assumptions called out explicitly?
- Does the intake avoid inventing unsupported details?
- Would a spec writer know what to clarify or preserve next?
