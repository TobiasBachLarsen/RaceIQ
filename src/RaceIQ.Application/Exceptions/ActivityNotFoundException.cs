namespace RaceIQ.Application.Exceptions;

public class ActivityNotFoundException : Exception
{
    public ActivityNotFoundException(int activityId)
        : base($"Activity {activityId} was not found.") { }
}
