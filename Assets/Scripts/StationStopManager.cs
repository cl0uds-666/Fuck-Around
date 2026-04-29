using UnityEngine;
using TMPro;

public class StationStopManager : MonoBehaviour
{
    [Header("References")]
    public TrainController train;
    public RouteData routeData; // NEW
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI resultText;

    [Header("Station Settings")]
    public float stopTolerance = 5f;       // +/- metres allowed
    public float stoppedSpeed = 0.5f;      // counts as stopped

    private bool hasCheckedStop = false;

    private void Update()
    {
        float stationDistance = routeData.stationX; // NEW

        float distanceToStation = stationDistance - train.distanceAlongRoute;

        if (distanceText != null)
        {
            distanceText.text = "Station: " + Mathf.Max(distanceToStation, 0f).ToString("0") + "m";
        }

        bool insideStopZone = Mathf.Abs(distanceToStation) <= stopTolerance;
        bool trainStopped = train.speed <= stoppedSpeed;

        if (!hasCheckedStop && insideStopZone && trainStopped)
        {
            resultText.text = "Perfect stop - passengers collected!";
            hasCheckedStop = true;
        }

        if (!hasCheckedStop && distanceToStation < -stopTolerance)
        {
            resultText.text = "Overshot station - passengers missed!";
            hasCheckedStop = true;
        }
    }
}