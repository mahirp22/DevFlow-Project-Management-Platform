using DevFlow.Domain.Enums;
namespace DevFlow.Domain.Entities;

public sealed class ProjectInvitation
{
    private ProjectInvitation() { }
    public ProjectInvitation(Guid projectId, string email, ProjectRole role, string tokenHash, Guid invitedBy)
    {
        ProjectId = projectId; Email = email.Trim().ToLowerInvariant(); Role = role;
        TokenHash = tokenHash; InvitedByUserId = invitedBy;
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProjectId { get; private set; }
    public string Email { get; private set; } = "";
    public ProjectRole Role { get; private set; }
    public string TokenHash { get; private set; } = "";
    public Guid InvitedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; private set; } = DateTimeOffset.UtcNow.AddDays(7);
    public DateTimeOffset? AcceptedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public void Accept() => AcceptedAt = DateTimeOffset.UtcNow;
    public void Revoke() => IsRevoked = true;
}
