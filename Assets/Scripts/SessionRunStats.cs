using UnityEngine;

public class SessionRunStats : MonoBehaviour
{
    public enum RunSeverity
    {
        Minor,
        Major,
        Critical
    }

    public static SessionRunStats Instance { get; private set; }

    [Header("Passenger Counters")]
    public int passengerPickups;
    public int passengerDropOffs;

    [Header("Stop Outcomes")]
    public int missedStops;
    public int accurateStops;
    public int dwellTimeViolations;
    public int spadCount;

    [Header("Braking Analysis")]
    public int harshBrakeCount;
    public int emergencyBrakeUsageCount;
    public float peakBrakeSeverity;
    public float peakDeceleration;
    public float peakJerk;

    [Header("Door Safety")]
    public int tractionWithDoorOpenViolations;
    public int offPlatformDoorOpenCommandViolations;
    public RunSeverity doorRunSeverity = RunSeverity.Minor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RecordPassengerPickup(int count)
    {
        if (count <= 0)
        {
            return;
        }

        passengerPickups += count;
        Debug.Log($"[SessionRunStats] Pickup +{count} (total {passengerPickups})");
    }

    public void RecordPassengerDropOff(int count)
    {
        if (count <= 0)
        {
            return;
        }

        passengerDropOffs += count;
        Debug.Log($"[SessionRunStats] Drop-off +{count} (total {passengerDropOffs})");
    }

    public void RecordAccurateStop()
    {
        accurateStops++;
        Debug.Log($"[SessionRunStats] Accurate stops: {accurateStops}");
    }

    public void RecordMissedStop()
    {
        missedStops++;
        Debug.Log($"[SessionRunStats] Missed stops: {missedStops}");
    }

    public void RecordDwellTimeViolation()
    {
        dwellTimeViolations++;
        Debug.Log($"[SessionRunStats] Dwell time violations: {dwellTimeViolations}");
    }

    public void RecordSpad()
    {
        spadCount++;
        Debug.Log($"[SessionRunStats] SPAD count: {spadCount}");
    }

    public void PrintRunSummary()
    {
        int totalDoorViolations = tractionWithDoorOpenViolations + offPlatformDoorOpenCommandViolations;
        doorRunSeverity = EvaluateDoorSeverity(totalDoorViolations);

        Debug.Log(
            "[SessionRunStats] Run Summary => " +
            $"pickups: {passengerPickups}, " +
            $"dropOffs: {passengerDropOffs}, " +
            $"accurateStops: {accurateStops}, " +
            $"missedStops: {missedStops}, " +
            $"dwellTimeViolations: {dwellTimeViolations}, " +
            $"spadCount: {spadCount}, " +
            $"harshBrakeCount: {harshBrakeCount}, " +
            $"emergencyBrakeUsageCount: {emergencyBrakeUsageCount}, " +
            $"peakBrakeSeverity: {peakBrakeSeverity:0.00}, " +
            $"peakDeceleration: {peakDeceleration:0.00}, " +
            $"peakJerk: {peakJerk:0.00}, " +
            $"tractionWithDoorOpenViolations: {tractionWithDoorOpenViolations}, " +
            $"offPlatformDoorOpenCommandViolations: {offPlatformDoorOpenCommandViolations}, " +
            $"doorRunSeverity: {doorRunSeverity.ToString().ToLowerInvariant()}");

        PrintDoorEventExplanations();
    }

    public void RecordHarshBrake(float severity, float deceleration, float jerk, string context)
    {
        harshBrakeCount++;
        peakBrakeSeverity = Mathf.Max(peakBrakeSeverity, severity);
        peakDeceleration = Mathf.Max(peakDeceleration, deceleration);
        peakJerk = Mathf.Max(peakJerk, Mathf.Abs(jerk));

        Debug.Log($"[SessionRunStats] Harsh brake #{harshBrakeCount} severity={severity:0.00} decel={deceleration:0.00} jerk={jerk:0.00} at {context}");
    }

    public void RecordEmergencyBrakeUsage(float severity, float deceleration, float jerk, string context)
    {
        emergencyBrakeUsageCount++;
        peakBrakeSeverity = Mathf.Max(peakBrakeSeverity, severity);
        peakDeceleration = Mathf.Max(peakDeceleration, deceleration);
        peakJerk = Mathf.Max(peakJerk, Mathf.Abs(jerk));

        Debug.Log($"[SessionRunStats] Emergency brake #{emergencyBrakeUsageCount} severity={severity:0.00} decel={deceleration:0.00} jerk={jerk:0.00} at {context}");
    }

    public void RecordTractionWithDoorOpenViolation(string context)
    {
        tractionWithDoorOpenViolations++;
        Debug.LogWarning($"[Door Safety] Traction applied while door not fully closed/locked. Count={tractionWithDoorOpenViolations}. {context}");
    }

    public void RecordOffPlatformDoorOpenCommandViolation(string context)
    {
        offPlatformDoorOpenCommandViolations++;
        Debug.LogWarning($"[Door Safety] Door-open command issued while off-platform. Count={offPlatformDoorOpenCommandViolations}. {context}");
    }

    private RunSeverity EvaluateDoorSeverity(int totalDoorViolations)
    {
        if (totalDoorViolations >= 5)
        {
            return RunSeverity.Critical;
        }

        if (totalDoorViolations >= 3)
        {
            return RunSeverity.Major;
        }

        return RunSeverity.Minor;
    }

    private void PrintDoorEventExplanations()
    {
        Debug.Log(
            "[Door Safety] End-of-run explanation: " +
            $"Traction-with-door-open events={tractionWithDoorOpenViolations}. " +
            "This event means traction was commanded while a door was not fully closed/locked.");

        Debug.Log(
            "[Door Safety] End-of-run explanation: " +
            $"Off-platform door-open command events={offPlatformDoorOpenCommandViolations}. " +
            "This event means a door-open command was issued while the train was outside a platform zone.");

        Debug.Log($"[Door Safety] Penalty severity for scoring: {doorRunSeverity.ToString().ToLowerInvariant()}.");
    }
}
