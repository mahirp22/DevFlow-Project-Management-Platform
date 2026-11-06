using System.Text.RegularExpressions;
using DevFlow.Domain.Validation;
namespace DevFlow.Domain.Entities;

public sealed class Project
{
    private Project() { }
    public Project(Guid workspaceId, string key, string name)
    {
        WorkspaceId = DomainGuard.RequiredId(workspaceId, nameof(workspaceId));
        Key = DomainGuard.RequiredText(key, 10, nameof(key)).ToUpperInvariant();
        if (!Regex.IsMatch(Key, "^[A-Z][A-Z0-9]{1,9}$"))
            throw new ArgumentException("Use 2–10 letters/digits, starting with a letter.", nameof(key));
        Name = DomainGuard.RequiredText(name, 100, nameof(name));
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public bool IsDeleted { get; private set; }
    public int NextTaskNumber { get; private set; } = 1;
    public int AllocateTaskNumber() => NextTaskNumber++;
    public void Update(string name, string? description, bool archived)
    {
        Name = DomainGuard.RequiredText(name, 100, nameof(name));
        Description = DomainGuard.OptionalText(description, 4000, nameof(description));
        IsArchived = archived;
    }
    public void Delete() => IsDeleted = true;
    public Guid WorkspaceId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
