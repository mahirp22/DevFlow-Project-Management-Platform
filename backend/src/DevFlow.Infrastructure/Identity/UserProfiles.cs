using DevFlow.Application.Identity;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Infrastructure.Identity;

public sealed class UserProfiles(DevFlowDbContext db) : IUserProfiles
{
    public Task<ProfileResponse?> FindAsync(string subject, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().Where(x => x.CognitoSubject == subject)
            .Select(x => new ProfileResponse(x.Id, x.DisplayName, x.Email))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProfileResponse> EnsureAsync(VerifiedIdentity identity, CancellationToken cancellationToken)
    {
        // Parameters are interpolated by EF, never concatenated into SQL.
        // Concurrent first logins converge on the unique subject without granting any memberships.
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "CognitoSubject", "DisplayName", "Email", "CreatedAt")
            VALUES ({id}, {identity.Subject}, {identity.DisplayName}, {identity.Email}, {createdAt})
            ON CONFLICT ("CognitoSubject") DO NOTHING
            """, cancellationToken);
        return (await FindAsync(identity.Subject, cancellationToken))!;
    }
}
