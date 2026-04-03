---
name: spec
description: "Write the primary implementation spec from intake and confirmed decisions. This document is the agent-facing SSOT and should contain the detail needed for downstream execution."
---

Write a detailed implementation spec that downstream agents can rely on without repeated back-and-forth.

Core intent:

- `spec.md` is the primary machine-readable and agent-readable source of truth.
- Put implementation detail, edge cases, constraints, and contracts here.
- Do not optimize this document for Jira readability.
- Jira will be generated later as a separate human-facing artifact.

Rules:

- Follow the template structure exactly unless a section is clearly not applicable.
- Be explicit about what is confirmed, what is assumed, and what remains open.
- Prefer concrete statements over abstract product language.
- Include enough detail that engineering, design, QA, and follow-up agents can act from this file.
- If the intake is ambiguous, record the ambiguity instead of hiding it.
- Do not invent backend contracts, fields, or UI states without evidence.

Important emphasis:

- Backend expectations should be precise enough to guide API, data, and validation work.
- Frontend expectations should be precise enough to guide screen structure, states, and interactions.
- Edge cases and non-goals should be visible so later agents do not overbuild.
- Keep human-friendly summary short, but keep implementation detail comprehensive.
