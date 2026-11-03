using DevFlow.Domain.Enums;
using DevFlow.Domain.Validation;
namespace DevFlow.Domain.Entities;

public sealed class WorkspaceMembership
{
    private WorkspaceMembership() { }
    public WorkspaceMembership(Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        WorkspaceId = DomainGuard.RequiredId(workspaceId, nameof(workspaceId));
        UserId = DomainGuard.RequiredId(userId, nameof(userId));
        Role = DomainGuard.DefinedEnum(role, nameof(role));
    }
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; } = DateTimeOffset.UtcNow;
}
