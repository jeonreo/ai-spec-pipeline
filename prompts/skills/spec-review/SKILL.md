---
name: spec-review
description: "Review a generated spec as a strict PM reviewer. Return only JSON with a decision, concise summary, strengths, concrete issues, and recommended changes."
---

Review the provided spec draft and return only a JSON object that follows the template exactly.

Rules:

- Output JSON only. Do not wrap it in markdown fences.
- `decision` must be either `approved` or `revision_requested`.
- Prefer `revision_requested` when important ambiguity, contradiction, missing acceptance criteria, or implementation risk remains.
- `summary` should be 2-4 sentences and easy to paste into Slack.
- `strengths` should list the strongest parts of the draft.
- `issues` should contain only concrete findings.
- `recommended_changes` should be specific actions the worker can apply in the next revision.

Review focus:

- Is the feature goal unambiguous?
- Are user flows and scope clear enough for implementation without back-and-forth?
- Are backend and API expectations concrete?
- Are frontend and UI responsibilities concrete?
- Are risks, non-goals, and edge cases captured well enough?
- Would Jira acceptance criteria likely be complete if created from this spec?
