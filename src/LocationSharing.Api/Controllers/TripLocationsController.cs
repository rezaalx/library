using LocationSharing.Api.Contracts.Requests;
using LocationSharing.Api.Contracts.Responses;
using LocationSharing.Api.Data;
using LocationSharing.Api.Models;
using LocationSharing.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationSharing.Api.Controllers;

[ApiController]
[Route("api/trips/{tripPublicId:guid}/locations")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class TripLocationsController(LocationSharingDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(LocationWriteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostLocation(
        [FromRoute] Guid tripPublicId,
        [FromBody] PostLocationRequest request,
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

        if (!TripRules.IsVisibleAndActive(trip, DateTimeOffset.UtcNow))
        {
            return this.ProblemWithTrace(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Trip is not active or outside its valid time window.");
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
            .FirstOrDefaultAsync(tm => tm.TripId == trip.Id && tm.MemberId == member.Id, cancellationToken);
        if (tripMember is null || !tripMember.IsActive)
        {
            return this.ProblemWithTrace(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Member is not active in this trip.");
        }

        var now = DateTimeOffset.UtcNow;
        var latest = await dbContext.MemberLocationLatests
            .FirstOrDefaultAsync(l => l.TripId == trip.Id && l.MemberId == member.Id, cancellationToken);

        if (latest is null)
        {
            latest = new MemberLocationLatest
            {
                PublicId = Guid.NewGuid(),
                TripId = trip.Id,
                MemberId = member.Id,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Accuracy = request.Accuracy,
                Speed = request.Speed,
                Heading = request.Heading,
                RecordedAt = request.RecordedAt,
                UpdatedOn = now
            };

            dbContext.MemberLocationLatests.Add(latest);
        }
        else
        {
            latest.Latitude = request.Latitude;
            latest.Longitude = request.Longitude;
            latest.Accuracy = request.Accuracy;
            latest.Speed = request.Speed;
            latest.Heading = request.Heading;
            latest.RecordedAt = request.RecordedAt;
            latest.UpdatedOn = now;
        }

        var history = new MemberLocationHistory
        {
            PublicId = Guid.NewGuid(),
            TripId = trip.Id,
            MemberId = member.Id,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Accuracy = request.Accuracy,
            Speed = request.Speed,
            Heading = request.Heading,
            RecordedAt = request.RecordedAt,
            CreatedOn = now
        };
        dbContext.MemberLocationHistories.Add(history);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new LocationWriteResponse
        {
            TripPublicId = trip.PublicId,
            MemberPublicId = member.PublicId,
            RecordedAt = request.RecordedAt,
            UpdatedOn = now
        });
    }

    [HttpGet("latest")]
    [ProducesResponseType(typeof(IEnumerable<LocationLatestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestLocations([FromRoute] Guid tripPublicId, CancellationToken cancellationToken)
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

        var latestLocations = await (
            from latest in dbContext.MemberLocationLatests.AsNoTracking()
            join tripMember in dbContext.TripMembers.AsNoTracking()
                on new { latest.TripId, latest.MemberId } equals new { tripMember.TripId, tripMember.MemberId }
            join member in dbContext.Members.AsNoTracking()
                on latest.MemberId equals member.Id
            where latest.TripId == trip.Id && tripMember.IsActive
            orderby latest.RecordedAt descending
            select new LocationLatestResponse
            {
                MemberPublicId = member.PublicId,
                TripPublicId = trip.PublicId,
                Latitude = latest.Latitude,
                Longitude = latest.Longitude,
                Accuracy = latest.Accuracy,
                Speed = latest.Speed,
                Heading = latest.Heading,
                RecordedAt = latest.RecordedAt,
                UpdatedOn = latest.UpdatedOn
            }).ToListAsync(cancellationToken);

        return Ok(latestLocations);
    }
}
