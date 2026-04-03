---
name: intake
description: "Turn an unstructured request into a concise intake document with goals, scope, risks, assumptions, and explicit open questions."
---

Analyze the raw request and convert it into a structured intake document.

Rules:

- Use only information that is present in the request.
- Do not invent product behavior, APIs, fields, user roles, or hidden constraints.
- If something is unclear, capture it as an open question instead of guessing.
- Keep the document concise and decision-oriented.
- Separate confirmed facts from assumptions and unresolved items.

What good intake looks like:

- The problem is stated clearly in 1-3 short paragraphs.
- Goals describe the outcome, not implementation details.
- Scope makes it obvious what is included and excluded.
- Risks call out ambiguity, dependencies, and operational concerns.
- Open questions are concrete and answerable.

Open question format:

```text
Q. <single concrete question>
A.
```

- Use this only for items that are truly unresolved.
- If there are no meaningful open questions, omit that section entirely.
