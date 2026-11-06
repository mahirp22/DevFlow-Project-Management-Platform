using System.Security.Cryptography;
using System.Text;
using DevFlow.Application.Identity;
using DevFlow.Application.Projects;
using DevFlow.Domain.Entities;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.Infrastructure.Projects;

public sealed class InvitationService(DevFlowDbContext db, AccessGuard guard) : IInvitationService
{
    private static InvitationDto Map(ProjectInvitation i) => new(i.Id, i.Email, i.Role, i.ExpiresAt, i.IsRevoked, i.AcceptedAt);
    public async Task<IReadOnlyList<InvitationDto>> List(string subject, Guid workspaceId, Guid projectId, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "manage", ct);
        return await db.Invitations.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)
            .Take(100).Select(x => new InvitationDto(x.Id, x.Email, x.Role, x.ExpiresAt, x.IsRevoked, x.AcceptedAt)).ToListAsync(ct);
    }
    public async Task<InvitationCreated> Create(string subject, Guid workspaceId, Guid projectId, InvitationRequest request, CancellationToken ct)
    {
        var project = await guard.Project(subject, workspaceId, projectId, "manage", ct);
        if (project.IsArchived) throw new RequestFailure(409, "Restore the project before inviting members.");
        var user = await guard.User(subject, ct);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invitation = new ProjectInvitation(projectId, request.Email, request.Role, Hash(token), user.Id);
        db.Add(invitation);
        await db.SaveChangesAsync(ct);
        return new(Map(invitation), token);
    }
    public async Task Revoke(string subject, Guid workspaceId, Guid projectId, Guid invitationId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await guard.Project(subject, workspaceId, projectId, "manage", ct);
        var invitation = (await db.Invitations.FromSqlInterpolated(
            $"SELECT * FROM \"ProjectInvitations\" WHERE \"Id\" = {invitationId} AND \"ProjectId\" = {projectId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new RequestFailure(404, "Invitation not found.");
        if (invitation.AcceptedAt is not null) throw new RequestFailure(409, "This invitation has already been accepted.");
        invitation.Revoke();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task<ProjectDto> Accept(VerifiedIdentity identity, string token, CancellationToken ct)
    {
        var user = await guard.User(identity.Subject, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var hash = Hash(token.ToLowerInvariant());
        var invitation = (await db.Invitations.FromSqlInterpolated(
            $"SELECT * FROM \"ProjectInvitations\" WHERE \"TokenHash\" = {hash} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (invitation is null || invitation.IsRevoked || invitation.AcceptedAt is not null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new RequestFailure(404, "This invitation is invalid, expired, or already used.");
        if (!string.Equals(invitation.Email, identity.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new RequestFailure(403, "Sign in with the verified email address this invitation was sent to.");
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == invitation.ProjectId && !x.IsDeleted && !x.IsArchived, ct)
            ?? throw new RequestFailure(404, "Project not found.");
        var joined = DateTimeOffset.UtcNow;
        // Never replace or elevate an existing member's role through an invitation.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WorkspaceMemberships" ("WorkspaceId", "UserId", "Role", "JoinedAt")
            VALUES ({project.WorkspaceId}, {user.Id}, 'Member', {joined})
            ON CONFLICT ("WorkspaceId", "UserId") DO NOTHING
            """, ct);
        var role = invitation.Role.ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ProjectMemberships" ("WorkspaceId", "ProjectId", "UserId", "Role", "JoinedAt")
            VALUES ({project.WorkspaceId}, {project.Id}, {user.Id}, {role}, {joined})
            ON CONFLICT ("ProjectId", "UserId") DO NOTHING
            """, ct);
        invitation.Accept();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return AccessGuard.Map(project);
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
