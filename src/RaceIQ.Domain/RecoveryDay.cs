namespace RaceIQ.Domain;

// One day's recovery reading from WHOOP: how ready the body was that morning. Date is the
// UTC calendar day the recovery was scored (WHOOP scores it when the rider wakes up), and
// a ride is paired with the recovery of the day it started on.
public class RecoveryDay
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public DateOnly Date { get; set; }

    // 0-100 %, WHOOP's headline readiness number.
    public int RecoveryScore { get; set; }

    // Heart-rate variability (RMSSD) in milliseconds.
    public double HrvMs { get; set; }
    public int RestingHeartRate { get; set; }

    // WHOOP's cycle id for the recovery - kept so a reading can be traced back to its source.
    public required string ProviderRecordId { get; set; }
}
