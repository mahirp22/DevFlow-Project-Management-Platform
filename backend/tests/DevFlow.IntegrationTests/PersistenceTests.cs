using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace DevFlow.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class PersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Migration_is_applied_and_matches_current_model()
    {
        await using var db = fixture.CreateContext();
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.False(db.Database.HasPendingModelChanges());
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken); // Applying it again is a no-op.
    }

    [Fact]
    public async Task Task_round_trips_dates_priority_and_assignee()
    {
        await using var db = fixture.CreateContext();
        var data = await TestData.AddProjectAsync(db);
        var task = new TaskItem(data.Project.Id, 1, "Ship foundation", data.User.Id,
            TaskPriority.High, new DateOnly(2027, 1, 15), data.User.Id);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var saved = await db.Tasks.SingleAsync(x => x.Id == task.Id, TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(2027, 1, 15), saved.DueDate);
        Assert.Equal(TaskPriority.High, saved.Priority);
        Assert.Equal(data.User.Id, saved.AssigneeUserId);
        Assert.Equal(TimeSpan.Zero, saved.CreatedAt.Offset);
        Assert.NotEqual(0u, saved.Version);
    }

    [Fact]
    public async Task Cognito_subject_must_be_unique()
    {
        await using var db = fixture.CreateContext();
        var subject = Guid.NewGuid().ToString();
        db.Users.AddRange(new UserProfile(subject, "One", "one@example.invalid"),
            new UserProfile(subject, "Two", "two@example.invalid"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Project_key_is_unique_within_workspace_but_reusable_in_another()
    {
        await using var db = fixture.CreateContext();
        var first = await TestData.AddProjectAsync(db);
        await TestData.AddProjectAsync(db); // TEST in a different workspace is valid.
        db.Projects.Add(new Project(first.Workspace.Id, "test", "Duplicate"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Project_membership_cannot_claim_a_different_workspace()
    {
        await using var db = fixture.CreateContext();
        var first = await TestData.AddProjectAsync(db);
        var second = await TestData.AddProjectAsync(db);
        db.ProjectMemberships.Add(new ProjectMembership(second.Workspace.Id, first.Project.Id, second.User.Id, ProjectRole.Member));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Project_membership_requires_workspace_membership()
    {
        await using var db = fixture.CreateContext();
        var first = await TestData.AddProjectAsync(db);
        var second = await TestData.AddProjectAsync(db);
        db.ProjectMemberships.Add(new ProjectMembership(first.Workspace.Id, first.Project.Id, second.User.Id, ProjectRole.Member));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Task_cannot_be_assigned_to_member_of_another_project()
    {
        await using var db = fixture.CreateContext();
        var first = await TestData.AddProjectAsync(db);
        var second = await TestData.AddProjectAsync(db);
        db.Tasks.Add(new TaskItem(first.Project.Id, 1, "Invalid assignment", first.User.Id, assigneeUserId: second.User.Id));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Task_number_is_unique_within_project()
    {
        await using var db = fixture.CreateContext();
        var data = await TestData.AddProjectAsync(db);
        db.Tasks.AddRange(new TaskItem(data.Project.Id, 1, "One", data.User.Id), new TaskItem(data.Project.Id, 1, "Two", data.User.Id));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Stale_task_update_cannot_overwrite_newer_update()
    {
        await using var firstDb = fixture.CreateContext();
        var data = await TestData.AddProjectAsync(firstDb);
        var task = new TaskItem(data.Project.Id, 1, "Original", data.User.Id);
        firstDb.Tasks.Add(task);
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var secondDb = fixture.CreateContext();
        var stale = await secondDb.Tasks.SingleAsync(x => x.Id == task.Id, TestContext.Current.CancellationToken);
        firstDb.Entry(task).Property(x => x.Title).CurrentValue = "First update";
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        secondDb.Entry(stale).Property(x => x.Title).CurrentValue = "Stale update";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync(TestContext.Current.CancellationToken));
        secondDb.ChangeTracker.Clear();
        Assert.Equal("First update", (await secondDb.Tasks.SingleAsync(x => x.Id == task.Id, TestContext.Current.CancellationToken)).Title);
    }

    [Fact]
    public async Task Deleting_workspace_does_not_cascade_through_project_data()
    {
        await using var db = fixture.CreateContext();
        var data = await TestData.AddProjectAsync(db);
        db.ChangeTracker.Clear(); // Verify PostgreSQL's constraint, not tracked EF relationships.
        db.Workspaces.Remove(data.Workspace);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        db.ChangeTracker.Clear();
        Assert.True(await db.Projects.AnyAsync(x => x.Id == data.Project.Id, TestContext.Current.CancellationToken));
    }
}
