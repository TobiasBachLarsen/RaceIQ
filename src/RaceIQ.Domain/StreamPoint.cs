namespace RaceIQ.Domain;

public record StreamPoint(
    int TimeSeconds,
    double? Watts,
    double? HeartRateBpm,
    double? CadenceRpm,
    double? AltitudeMeters,
    double DistanceMeters);
