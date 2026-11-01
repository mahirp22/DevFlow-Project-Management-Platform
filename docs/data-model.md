# Proposed data model — not implemented

Workspace is the tenant boundary. Each project belongs to one workspace; each task belongs to one project. The initial product allows one optional assignee per task.

| Entity | Main fields / relationships |
| --- | --- |
| UserProfile | UUID, unique Cognito subject, display name, email, timestamps; no password |
| Workspace | UUID, name, creator, timestamps |
| WorkspaceMembership | Workspace + user unique pair; Owner / Admin / Member |
| Project | UUID, workspace, unique project key within workspace, name, description, archive state |
| ProjectMembership | Project + user unique pair; Manager / Member / Viewer |
| ProjectInvitation | Project, email, role, token hash, expiry, inviter, accepted/revoked state |
| TaskItem | UUID, project, project-local number, title, description, status, priority, due date, assignee, reporter, position, concurrency token |
| TaskComment | Task, author, body, timestamps |
| TaskAttachment | Task, uploader, object key, filename, byte count, media type, pending/finalized state |
| TaskActivity | Task, actor, event type, relevant before/after values, timestamp |

Task statuses: ToDo, InProgress, InReview, Completed. Backward moves are allowed. Initial priorities: Low, Medium, High, Urgent. Use TaskItem in C# to avoid confusion with System.Threading.Tasks.Task.

## Integrity

- UUID primary keys and explicit foreign keys.
- UTC event timestamps; date-only deadlines until time-of-day is required.
- Unique membership pairs and project-local task numbers.
- Project members must be workspace members; assignees must be project members.
- Preserve a workspace owner during membership changes.
- Check tenant access on every query and mutation; constrain relationships in the database where practical.
- Store only hashed invitation tokens; enforce expiry, single acceptance and verified intended recipient.
- Define archive, deletion and historical actor retention behavior before creating cascade rules.

Indexes will target project/status, project/assignee, due date, and task/activity time. Board ordering is persisted within columns; moves update ordering atomically and validate a concurrency version. We will choose a simple ordering strategy in Phase 6 based on measured needs.

Analytics initially query tasks/activity for status counts, overdue tasks, workload and completion trends. No analytics table is required in Phase 1. Invitation links can initially be shared manually; automated email requires a separately chosen delivery service.
