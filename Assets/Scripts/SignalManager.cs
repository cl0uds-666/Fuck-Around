using UnityEngine;
using TMPro;

public class SignalManager : MonoBehaviour
{
    [Header("References")]
    public TrainController train;
    public RouteData routeData;
    public TextMeshProUGUI infoText;
    private bool trainInRedZone = false;

    [Header("Stop Rules")]
    public float stoppedSpeed = 0.5f;
    public float redStopTolerance = 10f;
    public float waitTimeBeforeGreen = 2f;

    private bool hasFailedSignal = false;
    private bool redSignalCleared = false;
    private float stoppedTimer = 0f;
    private float activeRedSignalX = -1f;

    public void SetTrainInRedZone(bool inside)
    {
        trainInRedZone = inside;
    }

    private void Update()
    {
        if (routeData == null || train == null)
        {
            return;
        }

        float trainDistance = train.distanceAlongRoute;

        bool hasRedAhead = routeData.TryGetNextRedSignal(trainDistance, out float redSignalDistance);
        bool hasYellowAhead = routeData.TryGetNextYellowSignal(trainDistance, out float yellowSignalDistance);

        if (!hasRedAhead)
        {
            if (infoText != null)
            {
                infoText.text = "GREEN - End of route";
            }

            return;
        }

        if (activeRedSignalX != redSignalDistance)
        {
            activeRedSignalX = redSignalDistance;
            redSignalCleared = false;
            hasFailedSignal = false;
            stoppedTimer = 0f;
        }

        bool atRedSignal = trainInRedZone;
        bool trainStopped = train.speed <= stoppedSpeed;
        bool passedRed = trainDistance > redSignalDistance + redStopTolerance;

        string message;

        if (redSignalCleared)
        {
            message = "GREEN - Proceed";
        }
        else if (!hasYellowAhead || trainDistance < yellowSignalDistance)
        {
            message = "GREEN";
        }
        else if (trainDistance < redSignalDistance)
        {
            message = "YELLOW - Prepare to stop";
        }
        else
        {
            message = "RED - STOP";
        }

        if (!hasFailedSignal && !redSignalCleared && atRedSignal && trainStopped)
        {
            stoppedTimer += Time.deltaTime;
            message = "Stopped at red signal - wait...";

            if (stoppedTimer >= waitTimeBeforeGreen)
            {
                redSignalCleared = true;
                message = "Signal cleared - proceed";
            }
        }
        else if (!atRedSignal || !trainStopped)
        {
            stoppedTimer = 0f;
        }

        if (!hasFailedSignal && !redSignalCleared && passedRed && train.speed > stoppedSpeed)
        {
            hasFailedSignal = true;
            message = "SPAD! You passed a red signal!";
        }

        if (infoText != null)
        {
            infoText.text = message;
        }
    }
}
