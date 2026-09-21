## Autonomy / Clarification Policy

Work autonomously by default.

Do not ask the user clarifying questions unless progress is genuinely blocked or continuing would create a meaningful risk of losing or corrupting user work.

When multiple reasonable implementation choices exist:

1. Inspect the existing codebase, scenes, prefabs, ScriptableObjects, tests, and current conventions.
2. Prefer the option most consistent with the current architecture and established behavior.
3. Prefer the smallest safe change.
4. Preserve serialized data, event IDs, save compatibility, scene references, and existing public behavior unless the task explicitly requires changing them.
5. If one option is clearly safer or better supported by the existing project, choose it automatically.
6. For low-risk ambiguity, make a reasonable assumption and continue.
7. Record important assumptions and decisions in the final summary instead of interrupting the user.

Do not pause merely to ask which reasonable implementation option the user prefers.

Do not ask for confirmation before ordinary:

- first-party code edits
- prefab or scene edits that are safely authored from the saved project state
- localization/catalog updates
- tests
- validation
- non-destructive asset wiring
- small refactors required by the requested feature

Interrupt the user only when one of the following is true:

- Unity has unsaved Editor, scene, or prefab changes that could be overwritten.
- The requested action would delete, replace, or irreversibly rewrite user-authored work.
- Required credentials, permissions, secrets, or external access are unavailable.
- Essential project data is missing and cannot be inferred safely from the repository.
- Two or more choices would materially change intended game behavior, player-facing design, save compatibility, or production data, and the existing project provides no clear basis for choosing.
- A destructive Git operation would be required.

### Unity Editor Handling

When Unity is open:

- Detect whether relevant scenes or prefabs have unsaved Editor changes before authoring those same assets externally.
- If there are unsaved changes, stop only the conflicting scene or prefab write.
- Continue safe code inspection, code edits, catalog validation, tests that do not conflict with the Editor, and other non-conflicting work when possible.
- Once the user confirms the relevant Unity work is saved, continue without asking again unless the state changes.
- Do not repeatedly ask whether Unity is saved or closed after the user already confirmed it.
- If batch tests genuinely require Unity to be closed, request closure only when actually necessary.

### Automatic Technical Decisions

For ordinary technical implementation decisions:

- Inspect existing usages and architecture first.
- Choose the safest option consistent with existing project conventions.
- Do not present multiple implementation choices to the user when one can be selected from repository evidence.
- Do not ask whether to keep or remove clearly obsolete code when project-wide usage search proves it is unused and an authoritative replacement already exists.
- Preserve backward compatibility where practical.
- Preserve serialized identifiers and stable numeric IDs.
- Preserve intentional numeric gaps when renumbering could break compatibility.
- Do not compact IDs merely for neatness.
- Do not introduce new managers, abstractions, frameworks, or duplicate systems just to resolve ambiguity.

If an assumption is low-risk and reversible, make the assumption and continue.

If an assumption could materially alter game design or destroy existing work, ask before proceeding.

### Tests / Validation

After implementation:

- Run the relevant available tests automatically when safe.
- Run relevant validation automatically when safe.
- Fix clear regressions introduced by the current change without asking for permission.
- Do not stop to ask whether obvious compile errors caused by the current change should be fixed.
- Do not broaden the task into unrelated cleanup because tests reveal pre-existing issues.
- Report pre-existing failures separately from failures introduced by the current work.

### Scope Discipline

Do not interrupt the user for optional improvements outside the requested scope.

If an unrelated issue is discovered:

- leave it unchanged unless it blocks the requested work
- mention it briefly in the final report if relevant
- do not ask whether it should also be fixed

Do not perform unrelated refactors while implementing a focused feature.

### Final Reporting

At the end of the task, report:

- what changed
- important implementation decisions or assumptions
- tests and validation performed
- anything intentionally left unchanged
- remaining risks or manual Unity checks, if any

Prefer reporting decisions after completing the work rather than asking the user to make routine technical decisions beforehand.