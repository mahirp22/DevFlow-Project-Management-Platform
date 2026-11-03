using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using DevFlow.Infrastructure.Persistence;
namespace DevFlow.IntegrationTests;

internal static class TestData
{
    public static async Task<(UserProfile User, Workspace Workspace, Project Project)> AddProjectAsync(DevFlowDbContext db)
    {
        var user = new UserProfile(Guid.NewGuid().ToString(), "Test User", "test@example.invalid");
        var workspace = new Workspace("Test Workspace", user.Id);
        var project = new Project(workspace.Id, "TEST", "Test Project");
        db.AddRange(user, workspace, new WorkspaceMembership(workspace.Id, user.Id, WorkspaceRole.Owner),
            project, new ProjectMembership(workspace.Id, project.Id, user.Id, ProjectRole.Manager));
        await db.SaveChangesAsync();
        return (user, workspace, project);
    }
}
