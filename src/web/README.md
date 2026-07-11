# SocketChat Web

The SocketChat web frontend: a React 19 + TypeScript + Vite SPA. Currently
ships the auth UI shell — `/login`, `/register`, `/forgot-password` (public)
and `/` (protected, placeholder home page) — routed with `react-router-dom`.
Forms are UI-only for now: submitting does nothing, and there's no real
authentication yet.

To view the protected `/` page without a backend, set a fake token in the
browser devtools console:

```js
localStorage.setItem('access_token', 'fake')
```

Any truthy value in `access_token` satisfies the route guard; removing it
(or clearing localStorage) bounces you back to `/login`.

## Commands

- `npm run dev` — start the Vite dev server with HMR.
- `npm run lint` — run Oxlint.
- `npm run build` — type-check (`tsc -b`) and build for production into `dist/`.

## Vite template notes

This project started from the standard Vite React + TypeScript template. If
you need type-aware lint rules, see the
[Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules)
and edit `.oxlintrc.json`.
