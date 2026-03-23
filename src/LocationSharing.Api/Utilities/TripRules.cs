using LocationSharing.Api.Models;

namespace LocationSharing.Api.Utilities;

public static class TripRules
{
    public static bool IsVisibleAndActive(Trip trip, DateTimeOffset now) =>
        trip.IsActive && now >= trip.StartTime && now <= trip.EndTime;
}
