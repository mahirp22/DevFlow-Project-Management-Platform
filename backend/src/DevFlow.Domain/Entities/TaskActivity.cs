namespace DevFlow.Domain.Entities;

public sealed class TaskActivity
{
    private TaskActivity() { }
    public TaskActivity(Guid taskId, Guid actorId, string summary)
    { TaskId = taskId; ActorUserId = actorId; Summary = summary; }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TaskId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Summary { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
