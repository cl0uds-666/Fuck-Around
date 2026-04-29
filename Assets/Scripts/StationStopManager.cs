using UnityEngine;
using TMPro;

public class StationStopManager : MonoBehaviour
{
    [Header("References")]
    public TrainController train;
    public RouteData routeData;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI resultText;

    [Header("Station Settings")]
    public float stopTolerance = 5f;
    public float stoppedSpeed = 0.5f;

    private int stationIndex = 0;

    private void Update()
    {
        if (routeData == null || train == null || routeData.stationPositions.Count == 0)
        {
            return;
        }

        while (stationIndex < routeData.stationPositions.Count &&
               train.distanceAlongRoute > routeData.stationPositions[stationIndex] + stopTolerance)
        {
            stationIndex++;
        }

        if (stationIndex >= routeData.stationPositions.Count)
        {
            if (distanceText != null)
            {
                distanceText.text = "Station: End of route";
            }

            return;
        }

        float stationDistance = routeData.stationPositions[stationIndex];
        float distanceToStation = stationDistance - train.distanceAlongRoute;

        if (distanceText != null)
        {
            distanceText.text = "Station: " + Mathf.Max(distanceToStation, 0f).ToString("0") + "m";
        }

        bool insideStopZone = Mathf.Abs(distanceToStation) <= stopTolerance;
        bool trainStopped = train.speed <= stoppedSpeed;

        if (insideStopZone && trainStopped)
        {
            resultText.text = "Perfect stop - passengers collected!";
            stationIndex++;
        }
        else if (distanceToStation < -stopTolerance)
        {
            resultText.text = "Overshot station - passengers missed!";
            stationIndex++;
        }
    }
}
