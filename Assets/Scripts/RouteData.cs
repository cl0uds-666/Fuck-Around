using UnityEngine;

public class RouteData : MonoBehaviour
{
    [Header("References")]
    public TrainController train;

    [Header("Station")]
    public float stationX = 800f;
    public float redBeforeStationDistance = 50f;

    [Header("Signal Spacing")]
    public float safetyMultiplier = 1.25f;
    public float minimumSignalSpacing = 100f;

    [Header("Calculated Signal Positions")]
    public float greenSignalX;
    public float yellowSignalX;
    public float redSignalX;

    private void Awake()
    {
        CalculateSignalPositions();
    }

    private void OnValidate()
    {
        CalculateSignalPositions();
    }

    public void CalculateSignalPositions()
    {
        if (train == null)
        {
            return;
        }

        float stoppingDistance = (train.maxSpeed * train.maxSpeed) / (2f * train.brakePower);
        float signalSpacing = stoppingDistance * safetyMultiplier;

        signalSpacing = Mathf.Max(signalSpacing, minimumSignalSpacing);

        redSignalX = stationX - redBeforeStationDistance;
        yellowSignalX = redSignalX - signalSpacing;
        greenSignalX = yellowSignalX - signalSpacing;
    }
}