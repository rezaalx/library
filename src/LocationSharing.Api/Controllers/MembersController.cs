using LocationSharing.Api.Contracts.Requests;
using LocationSharing.Api.Contracts.Responses;
using LocationSharing.Api.Data;
using LocationSharing.Api.Models;
using LocationSharing.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LocationSharing.Api.Controllers;

[ApiController]
[Route("api/members")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class MembersController(LocationSharingDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMember([FromBody] CreateMemberRequest request, CancellationToken cancellationToken)
    {
        var emailExists = await dbContext.Members.AnyAsync(m => m.Email == request.Email, cancellationToken);
        if (emailExists)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status409Conflict,
                "Conflict",
                "A member with this email already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var member = new Member
        {
            PublicId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName?.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            CreatedOn = now,
            UpdatedOn = now
        };

        dbContext.Members.Add(member);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            return this.ProblemWithTrace(
                StatusCodes.Status409Conflict,
                "Conflict",
                "A member with this email already exists.");
        }

        return CreatedAtAction(nameof(GetMemberByPublicId), new { memberPublicId = member.PublicId }, ToResponse(member));
    }

    [HttpGet("{memberPublicId:guid}")]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberByPublicId([FromRoute] Guid memberPublicId, CancellationToken cancellationToken)
    {
        var member = await dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.PublicId == memberPublicId, cancellationToken);

        if (member is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status404NotFound,
                "Not Found",
                "Member not found.");
        }

        return Ok(ToResponse(member));
    }

    private static MemberResponse ToResponse(Member member) =>
        new()
        {
            PublicId = member.PublicId,
            Name = member.Name,
            Email = member.Email,
            DisplayName = member.DisplayName,
            ImageUrl = member.ImageUrl,
            CreatedOn = member.CreatedOn,
            UpdatedOn = member.UpdatedOn
        };
}
