namespace RaceIQ.Domain;

public class RaceResult
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ConnectedAccountProvider Provider { get; set; }
    public required string ProviderResultId { get; set; }
    public required string EventName { get; set; }
    public DateTime EventDate { get; set; }
    public string? Category { get; set; }
    public int? Position { get; set; }
    public int? FieldSize { get; set; }
    public TimeSpan? Duration { get; set; }
    public double? RatingChange { get; set; }
    public int? ActivityId { get; set; }
}
