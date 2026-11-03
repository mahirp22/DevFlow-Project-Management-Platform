using DevFlow.Domain.Enums;
using DevFlow.Domain.Validation;
namespace DevFlow.Domain.Entities;

public sealed class ProjectMembership
{
    private ProjectMembership() { }
    public ProjectMembership(Guid workspaceId, Guid projectId, Guid userId, ProjectRole role)
    {
        WorkspaceId = DomainGuard.RequiredId(workspaceId, nameof(workspaceId));
        ProjectId = DomainGuard.RequiredId(projectId, nameof(projectId));
        UserId = DomainGuard.RequiredId(userId, nameof(userId));
        Role = DomainGuard.DefinedEnum(role, nameof(role));
    }
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; } = DateTimeOffset.UtcNow;
}
