using Microsoft.AspNetCore.Mvc.Filters;

namespace SAMA.Web.Authorization;

public static class AuthHelpers
{
    public static async Task<Guid?> GetWorkspaceIdAsync(AuthorizationFilterContext context)
    {
        // Try to get from route
        if (context.RouteData.Values.TryGetValue("workspaceId", out var wsId) &&
            Guid.TryParse(wsId?.ToString(), out var workspaceId))
        {
            return workspaceId;
        }

        // Try to get from query string for GET handlers
        if (context.HttpContext.Request.Query.TryGetValue("workspaceId", out var wsIdQuery) &&
            Guid.TryParse(wsIdQuery.ToString(), out var workspaceIdFromQuery))
        {
            return workspaceIdFromQuery;
        }

        // Try from query string (default Razor Pages behavior)
        var workspaceIdFromEntity = await GetWorkspaceIdFromEntityAsync(context, context.HttpContext.Request.Query);
        if (workspaceIdFromEntity != null)
        {
            return workspaceIdFromEntity;
        }

        return null;
    }

    private static async Task<Guid?> GetWorkspaceIdFromEntityAsync(AuthorizationFilterContext context, IQueryCollection query)
    {
        var page = context.RouteData.Values["page"]?.ToString() ?? string.Empty;
        Guid id = Guid.Empty;
        if (query.TryGetValue("id", out var idValues) &&
            Guid.TryParse(idValues.ToString(), out var parsedId))
        {
            id = parsedId;
        }

        if (page.StartsWith("/Workspaces/", StringComparison.OrdinalIgnoreCase) && id != Guid.Empty)
        {
            return id;
        }

        Guid checkId = Guid.Empty;
        if (query.TryGetValue("checkId", out var checkIdValues) &&
            Guid.TryParse(checkIdValues.ToString(), out var parsedCheckId))
        {
            checkId = parsedCheckId;
        }
        if ((page.StartsWith("/Checks/", StringComparison.OrdinalIgnoreCase) && id != Guid.Empty)
            || (page.StartsWith("/Alerts/", StringComparison.OrdinalIgnoreCase) && checkId != Guid.Empty))
        {
            var checkQuery = context.HttpContext.RequestServices
                .GetRequiredService<Services.Queries.CheckQueryService>();
            var check = await checkQuery.GetCheckBasicInfoAsync((id != Guid.Empty) ? id : checkId);
            return check?.WorkspaceId;
        }

        if (page.StartsWith("/NotificationChannels/", StringComparison.OrdinalIgnoreCase) && id != Guid.Empty)
        {
            var channelQuery = context.HttpContext.RequestServices
                .GetRequiredService<Services.Queries.ChannelQueryService>();
            var channel = await channelQuery.GetChannelDetailsAsync(id);
            return channel?.WorkspaceId;
        }

        if (page.StartsWith("/Alerts/", StringComparison.OrdinalIgnoreCase) && id != Guid.Empty)
        {
            var alertQuery = context.HttpContext.RequestServices
                .GetRequiredService<Services.Queries.AlertQueryService>();
            var alert = await alertQuery.GetAlertForEditAsync(id);
            return alert?.WorkspaceId;
        }

        return null;
    }
}
