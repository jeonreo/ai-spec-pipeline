---
name: jira-review
description: "Review a generated Jira ticket draft as a strict PM reviewer. Return only JSON with a decision, concise summary, strengths, concrete issues, and recommended changes."
---

Review the provided Jira draft and return only a JSON object that follows the template exactly.

Rules:

- Output JSON only. Do not wrap it in markdown fences.
- `decision` must be either `approved` or `revision_requested`.
- Prefer `revision_requested` when the summary is vague, the description is hard to scan, or the acceptance criteria would be hard for humans to execute.
- `summary` should be 2-4 sentences and easy to paste into Slack.
- `strengths` should list the strongest parts of the draft.
- `issues` should contain only concrete findings.
- `recommended_changes` should be specific actions the worker can apply in the next revision.

Review focus:

- Is the Jira title concise, human-readable, and easy to prioritize?
- Is the description easy for PMs, designers, engineers, and QA to skim quickly?
- Do the scope and non-goals match the spec without overloading the ticket?
- Are the acceptance criteria testable and free of duplicates?
- Would a human understand why this ticket exists within a few seconds?
