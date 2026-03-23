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
[Route("api/trips")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class TripsController(LocationSharingDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTrip([FromBody] CreateTripRequest request, CancellationToken cancellationToken)
    {
        var code = await GenerateUniqueCodeAsync(cancellationToken);
        if (code is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status409Conflict,
                "Conflict",
                "Unable to generate a unique join code.");
        }

        var now = DateTimeOffset.UtcNow;
        var trip = new Trip
        {
            PublicId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Title = request.Title?.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsActive = request.IsActive,
            StartLatitude = request.StartLatitude,
            StartLongitude = request.StartLongitude,
            EndLatitude = request.EndLatitude,
            EndLongitude = request.EndLongitude,
            Code = code,
            Description = request.Description?.Trim(),
            CreatedOn = now,
            UpdatedOn = now
        };

        dbContext.Trips.Add(trip);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            return this.ProblemWithTrace(
                StatusCodes.Status409Conflict,
                "Conflict",
                "Unable to create trip because of a uniqueness conflict. Retry request.");
        }

        return CreatedAtAction(nameof(GetTripByPublicId), new { tripPublicId = trip.PublicId }, ToTripResponse(trip));
    }

    [HttpGet("{tripPublicId:guid}")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTripByPublicId([FromRoute] Guid tripPublicId, CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == tripPublicId, cancellationToken);
        if (trip is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status404NotFound,
                "Not Found",
                "Trip not found.");
        }

        return Ok(ToTripResponse(trip));
    }

    [HttpPost("{tripPublicId:guid}/end")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EndTrip([FromRoute] Guid tripPublicId, CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips.FirstOrDefaultAsync(t => t.PublicId == tripPublicId, cancellationToken);
        if (trip is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status404NotFound,
                "Not Found",
                "Trip not found.");
        }

        trip.IsActive = false;
        trip.UpdatedOn = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToTripResponse(trip));
    }

    [HttpPost("join")]
    [ProducesResponseType(typeof(TripMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> JoinTrip([FromBody] JoinTripRequest request, CancellationToken cancellationToken)
    {
        var member = await dbContext.Members.FirstOrDefaultAsync(m => m.PublicId == request.MemberPublicId, cancellationToken);
        if (member is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status404NotFound,
                "Not Found",
                "Member not found.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var trip = await dbContext.Trips.FirstOrDefaultAsync(t => t.Code == normalizedCode, cancellationToken);
        if (trip is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status404NotFound,
                "Not Found",
                "Join code not found.");
        }

        if (!TripRules.IsVisibleAndActive(trip, DateTimeOffset.UtcNow))
        {
            return this.ProblemWithTrace(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Trip is not active or outside its valid time window.");
        }

        var existing = await dbContext.TripMembers
            .Include(tm => tm.Trip)
            .Include(tm => tm.Member)
            .FirstOrDefaultAsync(tm => tm.TripId == trip.Id && tm.MemberId == member.Id, cancellationToken);

        if (existing is not null && existing.IsActive)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status409Conflict,
                "Conflict",
                "Member already joined this trip.");
        }

        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            existing = new TripMember
            {
                PublicId = Guid.NewGuid(),
                MemberId = member.Id,
                TripId = trip.Id,
                IsActive = true,
                JoinedOn = now,
                Member = member,
                Trip = trip
            };
            dbContext.TripMembers.Add(existing);
        }
        else
        {
            existing.IsActive = true;
            existing.JoinedOn = now;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            return this.ProblemWithTrace(
                StatusCodes.Status409Conflict,
                "Conflict",
                "Member already joined this trip.");
        }
        return Ok(ToTripMemberResponse(existing));
    }

    [HttpPost("{tripPublicId:guid}/leave")]
    [ProducesResponseType(typeof(TripMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LeaveTrip(
        [FromRoute] Guid tripPublicId,
        [FromBody] LeaveTripRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips.FirstOrDefaultAsync(t => t.PublicId == tripPublicId, cancellationToken);
        if (trip is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status404NotFound,
                "Not Found",
                "Trip not found.");
        }

        var member = await dbContext.Members.FirstOrDefaultAsync(m => m.PublicId == request.MemberPublicId, cancellationToken);
        if (member is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status404NotFound,
                "Not Found",
                "Member not found.");
        }

        var tripMember = await dbContext.TripMembers
            .Include(tm => tm.Member)
            .Include(tm => tm.Trip)
            .FirstOrDefaultAsync(tm => tm.TripId == trip.Id && tm.MemberId == member.Id, cancellationToken);

        if (tripMember is null || !tripMember.IsActive)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Member is not active in this trip.");
        }

        tripMember.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToTripMemberResponse(tripMember));
    }

    [HttpGet("{tripPublicId:guid}/members")]
    [ProducesResponseType(typeof(IEnumerable<TripMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTripMembers([FromRoute] Guid tripPublicId, CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips.FirstOrDefaultAsync(t => t.PublicId == tripPublicId, cancellationToken);
        if (trip is null)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status404NotFound,
                "Not Found",
                "Trip not found.");
        }

        if (!TripRules.IsVisibleAndActive(trip, DateTimeOffset.UtcNow))
        {
            return this.ProblemWithTrace(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Trip is not active or outside its valid time window.");
        }

        var members = await dbContext.TripMembers
            .AsNoTracking()
            .Where(tm => tm.TripId == trip.Id && tm.IsActive)
            .Include(tm => tm.Member)
            .Include(tm => tm.Trip)
            .OrderBy(tm => tm.JoinedOn)
            .ToListAsync(cancellationToken);

        return Ok(members.Select(ToTripMemberResponse));
    }

    private async Task<string?> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = TripCodeGenerator.Generate(8);
            var exists = await dbContext.Trips.AnyAsync(t => t.Code == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return null;
    }

    private static TripResponse ToTripResponse(Trip trip) =>
        new()
        {
            PublicId = trip.PublicId,
            Name = trip.Name,
            Title = trip.Title,
            StartTime = trip.StartTime,
            EndTime = trip.EndTime,
            IsActive = trip.IsActive,
            StartLatitude = trip.StartLatitude,
            StartLongitude = trip.StartLongitude,
            EndLatitude = trip.EndLatitude,
            EndLongitude = trip.EndLongitude,
            Code = trip.Code,
            Description = trip.Description,
            CreatedOn = trip.CreatedOn,
            UpdatedOn = trip.UpdatedOn
        };

    private static TripMemberResponse ToTripMemberResponse(TripMember tripMember) =>
        new()
        {
            TripMemberPublicId = tripMember.PublicId,
            MemberPublicId = tripMember.Member.PublicId,
            TripPublicId = tripMember.Trip.PublicId,
            MemberName = tripMember.Member.Name,
            MemberEmail = tripMember.Member.Email,
            MemberDisplayName = tripMember.Member.DisplayName,
            IsActive = tripMember.IsActive,
            JoinedOn = tripMember.JoinedOn
        };
}
