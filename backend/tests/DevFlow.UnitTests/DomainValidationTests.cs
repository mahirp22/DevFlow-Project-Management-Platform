using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using TaskStatus = DevFlow.Domain.Enums.TaskStatus;
namespace DevFlow.UnitTests;

public sealed class DomainValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Workspace_rejects_blank_names(string name) =>
        Assert.Throws<ArgumentException>(() => new Workspace(name, Guid.NewGuid()));

    [Fact]
    public void Workspace_rejects_oversized_names() =>
        Assert.Throws<ArgumentException>(() => new Workspace(new string('x', 101), Guid.NewGuid()));

    [Fact]
    public void Workspace_requires_a_creator() =>
        Assert.Throws<ArgumentException>(() => new Workspace("Workspace", Guid.Empty));

    [Theory]
    [InlineData("1AB")]
    [InlineData("A")]
    [InlineData("TOO-LONG-KEY")]
    [InlineData("A B")]
    public void Project_rejects_invalid_keys(string key) =>
        Assert.Throws<ArgumentException>(() => new Project(Guid.NewGuid(), key, "Project"));

    [Fact]
    public void Project_normalizes_key_and_name()
    {
        var project = new Project(Guid.NewGuid(), " dev ", " Project ");
        Assert.Equal("DEV", project.Key);
        Assert.Equal("Project", project.Name);
    }

    [Fact]
    public void Membership_rejects_unknown_roles() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceMembership(Guid.NewGuid(), Guid.NewGuid(), (WorkspaceRole)99));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Task_requires_positive_number(int number) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskItem(Guid.NewGuid(), number, "Task", Guid.NewGuid()));

    [Fact]
    public void Task_rejects_oversized_title() =>
        Assert.Throws<ArgumentException>(() => new TaskItem(Guid.NewGuid(), 1, new string('x', 201), Guid.NewGuid()));

    [Fact]
    public void Task_rejects_undefined_priority() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskItem(Guid.NewGuid(), 1, "Task", Guid.NewGuid(), (TaskPriority)99));

    [Fact]
    public void New_task_is_unassigned_and_to_do()
    {
        var task = new TaskItem(Guid.NewGuid(), 1, " First task ", Guid.NewGuid());
        Assert.Equal(TaskStatus.ToDo, task.Status);
        Assert.Null(task.AssigneeUserId);
        Assert.Null(task.DueDate);
        Assert.Equal("First task", task.Title);
    }
}
