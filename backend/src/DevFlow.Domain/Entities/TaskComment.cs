using DevFlow.Domain.Validation;
namespace DevFlow.Domain.Entities;

public sealed class TaskComment
{
    private TaskComment() { }
    public TaskComment(Guid taskId, Guid authorId, string body)
    {
        TaskId = taskId; AuthorUserId = authorId;
        Body = DomainGuard.RequiredText(body, 4000, nameof(body));
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TaskId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
