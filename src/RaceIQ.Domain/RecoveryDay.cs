namespace RaceIQ.Domain;

// WHOOP's own traffic-light reading of a recovery score.
public enum RecoveryBand
{
    Low,
    Medium,
    High
}

// One day's recovery reading from WHOOP: how ready the body was that morning. Date is the
// UTC calendar day the recovery was scored (WHOOP scores it when the rider wakes up), and
// a ride is paired with the recovery of the day it started on.
public class RecoveryDay
{
    // WHOOP's bands: green from 67 %, yellow from 34 %, red below. Kept here so every
    // place that colours or describes a score agrees on the cut-offs.
    public const int HighThreshold = 67;
    public const int MediumThreshold = 34;

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

    public RecoveryBand Band =>
        RecoveryScore >= HighThreshold ? RecoveryBand.High
        : RecoveryScore >= MediumThreshold ? RecoveryBand.Medium
        : RecoveryBand.Low;
}
