using System.ComponentModel.DataAnnotations;
using DevFlow.Domain.Enums;
using TaskStatus = DevFlow.Domain.Enums.TaskStatus;
namespace DevFlow.Application.Projects;

public sealed record WorkspaceRequest([Required, StringLength(100, MinimumLength = 1)] string Name);
public sealed record ProjectRequest(
    [Required, RegularExpression("^[A-Za-z][A-Za-z0-9]{1,9}$")] string Key,
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [StringLength(4000)] string? Description, bool IsArchived = false);
public sealed record TaskRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [StringLength(10000)] string? Description,
    [EnumDataType(typeof(TaskPriority))] TaskPriority Priority = TaskPriority.Medium,
    [EnumDataType(typeof(TaskStatus))] TaskStatus Status = TaskStatus.ToDo,
    DateOnly? DueDate = null, Guid? AssigneeUserId = null, uint? Version = null);
public sealed record CommentRequest([Required, StringLength(4000, MinimumLength = 1)] string Body);
public sealed record InvitationRequest([Required, EmailAddress, StringLength(320)] string Email,
    [EnumDataType(typeof(ProjectRole))] ProjectRole Role);
public sealed record AcceptInvitationRequest([Required, RegularExpression("^[a-fA-F0-9]{64}$")] string Token);
public sealed record WorkspaceRoleRequest([EnumDataType(typeof(WorkspaceRole))] WorkspaceRole Role);
public sealed record ProjectRoleRequest([EnumDataType(typeof(ProjectRole))] ProjectRole Role);
public sealed class TaskFilter
{
    [StringLength(200)] public string? Search { get; set; }
    [EnumDataType(typeof(TaskStatus))] public TaskStatus? Status { get; set; }
    [EnumDataType(typeof(TaskPriority))] public TaskPriority? Priority { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public bool Overdue { get; set; }
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
}
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record WorkspaceDto(Guid Id, string Name, WorkspaceRole Role);
public sealed record MemberDto(Guid UserId, string DisplayName, string Email, string Role);
public sealed record ProjectDto(Guid Id, Guid WorkspaceId, string Key, string Name, string? Description, bool IsArchived);
public sealed record TaskDto(Guid Id, Guid ProjectId, Guid WorkspaceId, string ProjectKey, int Number, string Title,
    string? Description, TaskStatus Status, TaskPriority Priority, DateOnly? DueDate,
    Guid? AssigneeUserId, string? AssigneeName, uint Version, DateTimeOffset UpdatedAt);
public sealed record CommentDto(Guid Id, string Body, string Author, DateTimeOffset CreatedAt);
public sealed record ActivityDto(Guid Id, string Summary, string Actor, DateTimeOffset CreatedAt);
public sealed record InvitationDto(Guid Id, string Email, ProjectRole Role, DateTimeOffset ExpiresAt, bool IsRevoked, DateTimeOffset? AcceptedAt);
public sealed record InvitationCreated(InvitationDto Invitation, string Token);
public sealed record StatusCount(TaskStatus Status, int Count);
public sealed record WorkloadCount(string Assignee, int Count);
public sealed record AnalyticsDto(int Total, int Overdue, int Completed, IReadOnlyList<StatusCount> ByStatus, IReadOnlyList<WorkloadCount> Workload);

public sealed class RequestFailure(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed record BoardCard(TaskDto Task, long Position);
public sealed record BoardDto(string Revision, IReadOnlyList<BoardCard> Cards);
public sealed record MoveTaskRequest(
    [EnumDataType(typeof(TaskStatus))] TaskStatus Status,
    Guid? BeforeTaskId,
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$")] string Revision);
