# PA Tickets Phase 9.8 — White-first premium enterprise UI and defect inventory

## Scope and design direction

This phase focused on the Angular client’s visual system and the high-value ticket screens already in scope for the current contract: shell, home, login, ticket list, ticket detail, and ticket creation. The implementation kept all API contracts, authentication boundaries, and ticket operations intact and did not add Oracle, backend, or fake-data flows.

The visual direction follows the required enterprise UI mandate:

- White-first, premium enterprise surfaces
- Fluent-inspired blue and indigo accents
- Restricted glassmorphism, borders, depth, and soft shadowing
- Purposeful motion with reduced-motion support
- Consistent vector-style controls, empty states, and loading feedback
- Responsive, accessible form and table behavior

## Defect inventory

| Severity | Defect | Surface | Root cause | Resolution |
|---|---|---|---|---|
| Medium | Header and shell looked dark and heavy on a light app | Application shell | A dark, single-color header and minimal spacing created a mismatched visual hierarchy | Switched to a white-first shell with restrained blue/indigo highlights, softer borders, and a sticky glass-inspired top bar |
| Medium | Form, table, and detail surfaces lacked a cohesive system | List, detail, create, login | Each screen used ad hoc colors and radii with inconsistent spacing | Added a reusable design token set and standardized cards, table states, and buttons |
| Medium | Buttons, inputs, and actions felt low-confidence and visually unfinished | List, detail, create, login | Borderless or harsh contrast elements reduced clarity and polish | Unified rounded pill buttons, focus rings, hover states, and premium accent gradients |
| Low | Motion and reduced-motion support were not considered | Global UI | The app had no reduced-motion handling for accessibility | Added `prefers-reduced-motion` safeguards and kept transitions subtle and purposeful |
| Low | Empty and error states were visually generic | Status panels and lists | Status panels used minimal styling without hierarchy or emphasis | Enhanced status-card treatment with clearer emphasis and stronger empty-state visuals |

## Concrete visual defects identified before the redesign

1. The app shell used a dark blue header while the rest of the layout was mostly white, creating an abrupt visual break instead of a premium enterprise composition.
2. Cards, filters, and tables shared inconsistent border, radius, and spacing patterns, leading to a scattered UI rhythm.
3. Input fields and action buttons had minimal depth and feedback, reducing confidence in primary flows and making the app feel unfinished.
4. The default layout did not respect reduced-motion accessibility, which is required for enterprise-grade UX.
5. The login screen was visually closer to a generic form than a polished branded entry point.

## Implementation summary

- Centralized reusable design tokens and a global white-first theme in [client/src/styles.scss](../client/src/styles.scss).
- Reworked the app shell in [client/src/app/layout/shell.scss](../client/src/app/layout/shell.scss) for a premium, sticky, enterprise layout.
- Restyled the ticket list and ticket detail screens in [client/src/app/features/tickets/ticket-list.scss](../client/src/app/features/tickets/ticket-list.scss) and [client/src/app/features/tickets/ticket-detail.scss](../client/src/app/features/tickets/ticket-detail.scss).
- Reworked ticket creation and login screens in [client/src/app/features/tickets/create/ticket-create.scss](../client/src/app/features/tickets/create/ticket-create.scss) and [client/src/app/features/login/login.scss](../client/src/app/features/login/login.scss).
- Added regression coverage for the preserved list navigation context in [client/src/app/features/tickets/ticket-detail.page.spec.ts](../client/src/app/features/tickets/ticket-detail.page.spec.ts).

## UI areas delivered

- Application shell and navigation
- Ticket search and pagination
- Ticket detail and work-item completion flow
- Ticket creation and catalogue-driven form behaviour
- Login screen and sign-in handoff styling
- Shared status panels and empty/error feedback

## Validation

Commands executed from `client/`:

```text
npm test -- --no-watch --no-progress
npm run build
```

Actual results:

- Angular tests: 10 files passed, 26 tests passed, 0 failed, 0 skipped
- Production build: passed with 0 warnings and 0 errors

## Remaining risks and manual checks

- Live OIDC sign-in remains outside the current scope and is intentionally not implemented.
- Dynamic request field definitions remain intentionally hidden because the backend contract for field metadata is not available in this phase.
- Manual verification is still recommended to confirm the final look on the target browser widths and screen contrast settings.

## Gate status

- White-first enterprise redesign: PASS
- Reusable design system adoption: PASS
- Regression coverage for navigation context: PASS
- Angular test suite: PASS
- Production build: PASS
- Backend or Oracle changes: not introduced
