# Data model

Phase 2 implements UserProfile, Workspace, WorkspaceMembership, Project, ProjectMembership and TaskItem in the initial migration. Phase 5 adds invitations, comments and activity; Phase 7 adds TaskAttachment with uploader, generated S3 key, filename/type, byte length, SHA-256 checksum, upload expiry, finalization and cleanup timestamps. Read [Phase 2](phase-2.md) for mapping details and deliberate limitations.

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
| TaskComment | Task, author, body, creation timestamp |
| TaskAttachment | Task, uploader, object key, filename, byte count, media type, SHA-256, expiry, finalized/cleanup timestamps |
| TaskActivity | Task, actor, human-readable change summary, creation timestamp |

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

Indexes will target project/status, project/assignee, due date, and task/activity time. Board ordering is persisted within columns; moves update ordering atomically and validate a concurrency version. Phase 6 renumbers the destination column within a transaction and bounds boards to 500 active tasks.

Phase 5 analytics queries current tasks for status counts, overdue tasks and open workload. Historical completion trends are not implemented. No analytics table is required in Phase 1. Invitation links can initially be shared manually; automated email requires a separately chosen delivery service.

The implemented schema enforces workspace/project membership consistency with composite foreign keys, optional task assignees through project membership, unique project keys within workspaces, unique task numbers within projects, allowed enum values, and restrictive deletion. Task Version maps to PostgreSQL xmin. Phase 5 implements application authorization, last-owner preservation, UpdatedAt updates and transactional task-number allocation. Project and task soft deletion preserve history; deleted tasks clear their assignee. Project.NextTaskNumber is initialized from existing task numbers in the Phase 5 migration. Phase 6 implements ordering using the existing Position column, project-row locking, stable task-number tie breaking, and a revision derived from task IDs/xmin values. No additional migration is required.
