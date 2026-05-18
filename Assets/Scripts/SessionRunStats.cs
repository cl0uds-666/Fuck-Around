using UnityEngine;

public class SessionRunStats : MonoBehaviour
{
    public static SessionRunStats Instance { get; private set; }

    [Header("Passenger Counters")]
    public int passengerPickups;
    public int passengerDropOffs;

    [Header("Stop Outcomes")]
    public int missedStops;
    public int accurateStops;
    public int dwellTimeViolations;

    [Header("Braking Analysis")]
    public int harshBrakeCount;
    public int emergencyBrakeUsageCount;
    public float peakBrakeSeverity;
    public float peakDeceleration;
    public float peakJerk;

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

    public void PrintRunSummary()
    {
        Debug.Log(
            "[SessionRunStats] Run Summary => " +
            $"pickups: {passengerPickups}, " +
            $"dropOffs: {passengerDropOffs}, " +
            $"accurateStops: {accurateStops}, " +
            $"missedStops: {missedStops}, " +
            $"dwellTimeViolations: {dwellTimeViolations}, " +
            $"harshBrakeCount: {harshBrakeCount}, " +
            $"emergencyBrakeUsageCount: {emergencyBrakeUsageCount}, " +
            $"peakBrakeSeverity: {peakBrakeSeverity:0.00}, " +
            $"peakDeceleration: {peakDeceleration:0.00}, " +
            $"peakJerk: {peakJerk:0.00}");
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
}
