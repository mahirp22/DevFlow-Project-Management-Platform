using DevFlow.Domain.Validation;
namespace DevFlow.Domain.Entities;

public sealed class UserProfile
{
    private UserProfile() { }
    public UserProfile(string cognitoSubject, string displayName, string email)
    {
        CognitoSubject = DomainGuard.RequiredText(cognitoSubject, 128, nameof(cognitoSubject));
        DisplayName = DomainGuard.RequiredText(displayName, 100, nameof(displayName));
        Email = DomainGuard.RequiredText(email, 320, nameof(email));
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string CognitoSubject { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
