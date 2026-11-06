using DevFlow.Application.Identity;
namespace DevFlow.Application.Projects;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceDto>> List(string subject, CancellationToken ct);
    Task<WorkspaceDto> Create(string subject, WorkspaceRequest request, CancellationToken ct);
    Task Rename(string subject, Guid workspaceId, WorkspaceRequest request, CancellationToken ct);
    Task<IReadOnlyList<MemberDto>> Members(string subject, Guid workspaceId, CancellationToken ct);
    Task ChangeRole(string subject, Guid workspaceId, Guid userId, WorkspaceRoleRequest request, CancellationToken ct);
}
public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> List(string subject, Guid workspaceId, CancellationToken ct);
    Task<ProjectDto> Get(string subject, Guid workspaceId, Guid projectId, CancellationToken ct);
    Task<ProjectDto> Create(string subject, Guid workspaceId, ProjectRequest request, CancellationToken ct);
    Task<ProjectDto> Update(string subject, Guid workspaceId, Guid projectId, ProjectRequest request, CancellationToken ct);
    Task Delete(string subject, Guid workspaceId, Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<MemberDto>> Members(string subject, Guid workspaceId, Guid projectId, CancellationToken ct);
    Task ChangeRole(string subject, Guid workspaceId, Guid projectId, Guid userId, ProjectRoleRequest request, CancellationToken ct);
    Task RemoveMember(string subject, Guid workspaceId, Guid projectId, Guid userId, CancellationToken ct);
}
public interface ITaskService
{
    Task<PageResult<TaskDto>> List(string subject, Guid workspaceId, Guid projectId, TaskFilter filter, CancellationToken ct);
    Task<PageResult<TaskDto>> Mine(string subject, TaskFilter filter, CancellationToken ct);
    Task<TaskDto> Get(string subject, Guid workspaceId, Guid projectId, Guid taskId, CancellationToken ct);
    Task<TaskDto> Create(string subject, Guid workspaceId, Guid projectId, TaskRequest request, CancellationToken ct);
    Task<TaskDto> Update(string subject, Guid workspaceId, Guid projectId, Guid taskId, TaskRequest request, CancellationToken ct);
    Task Delete(string subject, Guid workspaceId, Guid projectId, Guid taskId, uint version, CancellationToken ct);
    Task<PageResult<CommentDto>> Comments(string subject, Guid workspaceId, Guid projectId, Guid taskId, int page, CancellationToken ct);
    Task<CommentDto> Comment(string subject, Guid workspaceId, Guid projectId, Guid taskId, CommentRequest request, CancellationToken ct);
    Task<PageResult<ActivityDto>> Activity(string subject, Guid workspaceId, Guid projectId, Guid taskId, int page, CancellationToken ct);
    Task<AnalyticsDto> Analytics(string subject, Guid workspaceId, Guid projectId, CancellationToken ct);
}
public interface IInvitationService
{
    Task<IReadOnlyList<InvitationDto>> List(string subject, Guid workspaceId, Guid projectId, CancellationToken ct);
    Task<InvitationCreated> Create(string subject, Guid workspaceId, Guid projectId, InvitationRequest request, CancellationToken ct);
    Task Revoke(string subject, Guid workspaceId, Guid projectId, Guid invitationId, CancellationToken ct);
    Task<ProjectDto> Accept(VerifiedIdentity identity, string token, CancellationToken ct);
}

public interface IBoardService
{
    Task<BoardDto> Get(string subject, Guid workspaceId, Guid projectId, CancellationToken ct);
    Task<BoardDto> Move(string subject, Guid workspaceId, Guid projectId, Guid taskId, MoveTaskRequest request, CancellationToken ct);
}
