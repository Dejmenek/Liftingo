# Liftingo frontend

## Tech stack

- Angular 22 + TypeScript
- PWA via Angular service worker
- Dexie.js (IndexedDB wrapper) for offline storage
- Playwright for e2e tests
- Azure Static Web Apps for hosting
- GitHub Actions for CI/CD (build, test, deploy)

## Conventions

- Standalone components only.
- Signal Forms for anything validated or multi-step.
- State lives in signals: component-local by default, promoted to a feature-level state service only when genuinely shared across components.
- Use `rxResource()` for reads against the NSwag-generated client; never `httpResource()`.
- Never hand-write an HTTP service or DTO that duplicates what NSwag already generates from the backend's OpenAPI spec.
- Styling is Tailwind only. Accessibility and interaction behavior comes from Angular Aria (tabs, listbox, combobox, accordion, menu) and Angular CDK (overlay/dialog, drag-and-drop, focus trap, live announcer, stepper).
- Follow the current (v20+) Angular style guide default: no type suffixes on files or classes.
- Interceptors and guards use the functional style.

## Common commands

```bash
cd frontend
npm install
ng serve
ng build
ng test
npx playwright test
```

Generating code (standalone by default, no type suffix per the current style guide):

```bash
ng generate component features/workout-log/session-summary
ng generate service features/plans/plan-state
ng generate guard core/auth/auth --functional
ng generate interceptor core/http/auth-token --functional
```

Regenerating the NSwag API client from the backend's OpenAPI spec (run after any backend endpoint change):

```bash
nswag run nswag.json
```
