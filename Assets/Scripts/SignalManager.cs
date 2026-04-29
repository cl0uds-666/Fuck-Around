using UnityEngine;
using TMPro;

public class SignalManager : MonoBehaviour
{
    [Header("References")]
    public TrainController train;
    public RouteData routeData;
    public TextMeshProUGUI infoText;
    private bool trainInRedZone = false;

    [Header("Signal Visuals")]
    public SignalVisual redSignalVisual;

    [Header("Green Visual Settings")]
    public Material greenMat;
    public string greenHex = "#00FF00";

    [Header("Stop Rules")]
    public float stoppedSpeed = 0.5f;
    public float redStopTolerance = 10f;
    public float waitTimeBeforeGreen = 2f;

    private bool hasFailedSignal = false;
    private bool redSignalCleared = false;
    private float stoppedTimer = 0f;

    public void SetTrainInRedZone(bool inside)
    {
        trainInRedZone = inside;
    }

    private void Update()
    {
        float yellowSignalDistance = routeData.yellowSignalX;
        float redSignalDistance = routeData.redSignalX;

        float trainDistance = train.distanceAlongRoute;
        float distanceToRed = redSignalDistance - trainDistance;

        bool atRedSignal = trainInRedZone;
        bool trainStopped = train.speed <= stoppedSpeed;
        bool passedRed = trainDistance > redSignalDistance + redStopTolerance;

        string message = "";

        if (redSignalCleared)
        {
            message = "GREEN - Proceed";
        }
        else if (trainDistance < yellowSignalDistance)
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

        // Stopped correctly at red
        if (!hasFailedSignal && !redSignalCleared && atRedSignal && trainStopped)
        {
            stoppedTimer += Time.deltaTime;
            message = "Stopped at red signal - wait...";

            if (stoppedTimer >= waitTimeBeforeGreen)
            {
                redSignalCleared = true;

                if (redSignalVisual != null)
                {
                    redSignalVisual.SetSignal(greenMat, greenHex);
                }

                message = "Signal cleared - proceed";
            }
        }
        else if (!atRedSignal || !trainStopped)
        {
            stoppedTimer = 0f;
        }

        // SPAD fail
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