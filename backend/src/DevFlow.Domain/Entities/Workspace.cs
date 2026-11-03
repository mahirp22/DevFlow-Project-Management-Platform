using DevFlow.Domain.Validation;
namespace DevFlow.Domain.Entities;

public sealed class Workspace
{
    private Workspace() { }
    public Workspace(string name, Guid createdByUserId)
    {
        Name = DomainGuard.RequiredText(name, 100, nameof(name));
        CreatedByUserId = DomainGuard.RequiredId(createdByUserId, nameof(createdByUserId));
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
