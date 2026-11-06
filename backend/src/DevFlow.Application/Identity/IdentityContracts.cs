using System.ComponentModel.DataAnnotations;
namespace DevFlow.Application.Identity;

public sealed record VerifiedIdentity(string Subject, string Email, string DisplayName);
public sealed record UpdateProfileRequest([Required, StringLength(100, MinimumLength = 1)] string DisplayName);
public sealed record ProfileResponse(Guid Id, string DisplayName, string Email);
public sealed record ResourceAccess(bool CanRead, bool CanWrite, bool CanManage);

public interface IUserProfiles
{
    Task<ProfileResponse?> RenameAsync(string subject, string displayName, CancellationToken cancellationToken);
    Task<ProfileResponse?> FindAsync(string subject, CancellationToken cancellationToken);
    Task<ProfileResponse> EnsureAsync(VerifiedIdentity identity, CancellationToken cancellationToken);
}

public interface IResourceAccess
{
    // A null result conceals both nonexistent resources and resources outside the caller's memberships.
    Task<ResourceAccess?> WorkspaceAsync(string subject, Guid workspaceId, CancellationToken cancellationToken);
    Task<ResourceAccess?> ProjectAsync(string subject, Guid workspaceId, Guid projectId, CancellationToken cancellationToken);
}

public interface IIdentityDirectory
{
    Task<VerifiedIdentity?> GetVerifiedAsync(string accessToken, string subject, CancellationToken cancellationToken);
}

public sealed class IdentityProviderUnavailableException : Exception
{
    public IdentityProviderUnavailableException() : base("The identity provider is unavailable.") { }
}
