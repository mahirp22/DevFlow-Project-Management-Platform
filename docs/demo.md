# DevFlow demo walkthrough

## Prepare without modifying real team data

Run your existing local PostgreSQL, API and Vite setup. Use http://localhost:5173. Confirm Connection reports healthy API/database. Use two Cognito accounts you control; set their display names through Account. Do not record passwords, login codes, AWS console credentials, invitation tokens or signed download URLs.

Create a new demo workspace named **DevFlow Studio** and project **Portfolio Launch** with key **PORT**. Add a few sample tasks through the UI:

| Task | Priority | Status |
| --- | --- | --- |
| Outline the product walkthrough | Medium | To Do |
| Review keyboard navigation | High | In Progress |
| Verify attachment permissions | High | In Review |
| Set up continuous integration | Medium | Completed |

Use dates appropriate to the day you record. Invite your second account and accept the invitation off-camera. Create a small text file containing only demo content for attachment testing. This guide does not seed/delete databases or change existing projects automatically.

## Five-minute recording

1. **0:00–0:30:** Explain the team coordination problem and show the overview/projects. State that the app runs locally with Cognito/S3 integrations.
2. **0:30–1:20:** Open Portfolio Launch. Create or edit a task, assign a teammate, set priority/deadline and filter the task list.
3. **1:20–2:10:** Put the second account's browser beside the first. Move a card and show the other account updating. Demonstrate the labelled Move controls as an alternative to dragging.
4. **2:10–3:00:** Add a comment, show activity history, upload the sample text file and download it. Keep signed URLs out of the recording.
5. **3:00–3:40:** Set the second account to Viewer. Demonstrate readable project data and unavailable write operations. Explain that the API enforces the same rules.
6. **3:40–4:30:** Open GitHub's successful CI run. Briefly explain frontend tests, database integration tests and container readiness.
7. **4:30–5:00:** Explain one tradeoff: single-instance SignalR or local hosting instead of paid ECS/RDS. End with a short next-step plan.

## Capture real screenshots

Capture a desktop board with tasks, task details/comments/attachment, the two-account collaboration view, and a narrow/mobile layout. Crop browser chrome and private account details as needed. Do not use generated mockups as evidence of implemented UI. Add only real screenshots to a future `docs/images/` directory and link them from the README after reviewing them. No screenshots or video are claimed to exist yet.

## Final manual checks

- At a narrow viewport, open Menu, tab through links, press Escape and confirm focus returns to Menu.
- Navigate with keyboard only; verify visible focus, labelled controls, skip-to-content behavior and board Move controls.
- At 200% zoom, verify text/actions remain usable. Check a real mobile viewport for unintended horizontal overflow.
- Start at Overview with DevTools Network open. Project/board chunks should load only when those features are visited; ensure normal navigation still works.
- Confirm failed API requests retain their error UI, and sign-out removes access to protected routes.
- Run the Phase 10 automated checks before the final commit. Do not call visual/accessibility checks passed unless you actually performed them.

These manual checks and optional media capture are not replaced by the automated test suite. Record observations rather than inventing a score or blanket accessibility claim.
