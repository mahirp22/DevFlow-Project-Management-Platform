using DevFlow.Domain.Enums;
using DevFlow.Domain.Validation;
using TaskStatus = DevFlow.Domain.Enums.TaskStatus;
namespace DevFlow.Domain.Entities;

public sealed class TaskItem
{
    private TaskItem() { }
    public TaskItem(Guid projectId, int number, string title, Guid reporterUserId,
        TaskPriority priority = TaskPriority.Medium, DateOnly? dueDate = null, Guid? assigneeUserId = null)
    {
        ProjectId = DomainGuard.RequiredId(projectId, nameof(projectId));
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        Number = number;
        Title = DomainGuard.RequiredText(title, 200, nameof(title));
        ReporterUserId = DomainGuard.RequiredId(reporterUserId, nameof(reporterUserId));
        Priority = DomainGuard.DefinedEnum(priority, nameof(priority));
        DueDate = dueDate;
        AssigneeUserId = assigneeUserId is { } id ? DomainGuard.RequiredId(id, nameof(assigneeUserId)) : null;
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public bool IsDeleted { get; private set; }
    public void Update(string title, string? description, TaskStatus status, TaskPriority priority, DateOnly? dueDate, Guid? assignee)
    {
        Title = DomainGuard.RequiredText(title, 200, nameof(title));
        Description = DomainGuard.OptionalText(description, 10000, nameof(description));
        Status = DomainGuard.DefinedEnum(status, nameof(status));
        Priority = DomainGuard.DefinedEnum(priority, nameof(priority));
        DueDate = dueDate;
        AssigneeUserId = assignee is { } id ? DomainGuard.RequiredId(id, nameof(assignee)) : null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    public void Move(TaskStatus status, long position)
    {
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        Status = DomainGuard.DefinedEnum(status, nameof(status));
        Position = position;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    public void Delete() { IsDeleted = true; AssigneeUserId = null; UpdatedAt = DateTimeOffset.UtcNow; }
    public Guid ProjectId { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskStatus Status { get; private set; } = TaskStatus.ToDo;
    public TaskPriority Priority { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public long Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public uint Version { get; private set; }
}
