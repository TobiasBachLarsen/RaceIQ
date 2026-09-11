namespace RaceIQ.Domain;

public class AnalysisReport
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public required string ReportText { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
