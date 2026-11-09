using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvTicket.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(AnalyticsService analytics, CurrentUser currentUser) : ControllerBase
{
    [HttpGet("organizer")]
    [Authorize(Roles = "Organizer,Admin")]
    public Task<OrganizerDashboardDto> Organizer(CancellationToken ct) =>
        analytics.GetOrganizerDashboardAsync(currentUser.RequireId(), ct);

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public Task<AdminDashboardDto> Admin(CancellationToken ct) =>
        analytics.GetAdminDashboardAsync(ct);

    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public Task<PagedResult<AdminUserDto>> Users(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        CancellationToken ct = default) =>
        analytics.GetUsersAsync(page, pageSize, q, ct);

    [HttpPost("users/{id:guid}/organizer")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetOrganizer(Guid id, [FromQuery] bool promote, CancellationToken ct)
    {
        await analytics.SetOrganizerAsync(id, promote, ct);
        return NoContent();
    }

    [HttpDelete("users/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        await analytics.DeleteUserAsync(id, ct);
        return NoContent();
    }
}
