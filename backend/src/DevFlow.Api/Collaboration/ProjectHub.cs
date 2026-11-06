using System.Collections.Concurrent;
using DevFlow.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevFlow.Api.Collaboration;

public sealed record ProjectSubscription(string ConnectionId, string Subject, Guid WorkspaceId, Guid ProjectId, DateTimeOffset ExpiresAt);

// One subscription per connection. This intentionally supports one API instance.
public sealed class ProjectConnections
{
    private readonly ConcurrentDictionary<string, ProjectSubscription> subscriptions = new();
    public void Set(ProjectSubscription subscription) => subscriptions[subscription.ConnectionId] = subscription;
    public void Remove(string connectionId) => subscriptions.TryRemove(connectionId, out _);
    public ProjectSubscription[] Snapshot() => subscriptions.Values.ToArray();
}

[Authorize]
public sealed class ProjectHub(ProjectConnections connections, IResourceAccess access) : Hub
{
    public async Task Subscribe(Guid workspaceId, Guid projectId)
    {
        connections.Remove(Context.ConnectionId);
        var subject = Context.User!.FindFirst("sub")!.Value;
        var allowed = await access.ProjectAsync(subject, workspaceId, projectId, Context.ConnectionAborted);
        if (allowed?.CanRead != true) throw new HubException("Project access is unavailable.");
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(Context.User.FindFirst("exp")!.Value));
        if (expiresAt <= DateTimeOffset.UtcNow) throw new HubException("Sign in again.");
        connections.Set(new(Context.ConnectionId, subject, workspaceId, projectId, expiresAt));
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed class ProjectNotifications(ProjectConnections connections, IResourceAccess access, IHubContext<ProjectHub> hub)
{
    public async Task Publish(Guid? workspaceId, Guid? projectId, CancellationToken ct)
    {
        foreach (var subscription in connections.Snapshot().Where(x =>
            (workspaceId is null || x.WorkspaceId == workspaceId) && (projectId is null || x.ProjectId == projectId)))
        {
            if (subscription.ExpiresAt <= DateTimeOffset.UtcNow)
            { connections.Remove(subscription.ConnectionId); continue; }
            // Group membership alone is insufficient: a role or membership may have changed since connection.
            var permission = await access.ProjectAsync(subscription.Subject, subscription.WorkspaceId, subscription.ProjectId, ct);
            if (permission?.CanRead != true)
            {
                connections.Remove(subscription.ConnectionId);
                await hub.Clients.Client(subscription.ConnectionId).SendAsync("AccessRevoked", ct);
            }
            else
                // No content or identity is broadcast. Clients fetch through authorized REST endpoints.
                await hub.Clients.Client(subscription.ConnectionId).SendAsync("ProjectChanged", ct);
        }
    }
}
