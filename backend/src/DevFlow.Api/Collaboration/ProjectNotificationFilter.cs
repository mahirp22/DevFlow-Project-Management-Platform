using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
namespace DevFlow.Api.Collaboration;

// Controllers return only after their use cases commit. Failed writes never emit change notifications.
public sealed class ProjectNotificationFilter(ProjectNotifications notifications, ILogger<ProjectNotificationFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null || HttpMethods.IsGet(context.HttpContext.Request.Method)) return;
        var status = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? 200;
        if (status is < 200 or >= 300) return;
        Guid? Id(string key) => context.ActionArguments.TryGetValue(key, out var value) && value is Guid id ? id : null;
        var workspace = Id("workspaceId");
        var project = Id("projectId");
        // Profile renames affect names throughout the app; invitation acceptance returns its project.
        if (executed.Result is ObjectResult { Value: DevFlow.Application.Projects.ProjectDto created })
        { workspace = created.WorkspaceId; project = created.Id; }
        if (workspace is null && context.Controller is not Controllers.MeController) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await notifications.Publish(workspace, project, timeout.Token);
        }
        catch (Exception exception)
        {
            // A committed REST write must not become a failed response because delivery failed.
            logger.LogWarning("Project notification failed with {ExceptionType}", exception.GetType().Name);
        }
    }
}
