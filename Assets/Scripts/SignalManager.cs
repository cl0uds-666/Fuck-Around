using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SignalManager : MonoBehaviour
{
    [Header("References")]
    public TrainController train;
    public RouteData routeData;
    public TextMeshProUGUI infoText;
    private bool trainInRedZone = false;

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
    private float activeRedSignalX = -1f;
    private float previousFrontPosition = float.MinValue;
    private bool trainStoppedInsideRedZone = false;

    private enum ActiveSignalState
    {
        Green,
        Yellow,
        Danger
    }

    private ActiveSignalState activeSignalState = ActiveSignalState.Green;

    private readonly Dictionary<float, SignalVisual> redSignalVisuals = new Dictionary<float, SignalVisual>();

    public void RegisterRedSignalVisual(float redSignalX, SignalVisual visual)
    {
        if (visual != null)
        {
            redSignalVisuals[redSignalX] = visual;
        }
    }

    public void SetTrainInRedZone(bool inside)
    {
        if (inside)
        {
            trainStoppedInsideRedZone = false;
        }

        trainInRedZone = inside;
    }

    public void EnterRedZone(float redSignalX)
    {
        trainInRedZone = true;
        trainStoppedInsideRedZone = false;

        if (activeRedSignalX != redSignalX)
        {
            activeRedSignalX = redSignalX;
            redSignalCleared = false;
            hasFailedSignal = false;
            stoppedTimer = 0f;
        }
    }

    public void EvaluateRedZoneExit()
    {
        if (hasFailedSignal || redSignalCleared)
        {
            return;
        }

        if (trainStoppedInsideRedZone)
        {
            return;
        }

        hasFailedSignal = true;

        if (SessionRunStats.Instance != null)
        {
            SessionRunStats.Instance.RecordSpadViolation(train != null ? train.distanceAlongRoute : activeRedSignalX);
        }

        if (infoText != null)
        {
            infoText.text = "SPAD! You passed a red signal!";
        }
    }

    private void Update()
    {
        if (routeData == null || train == null)
        {
            return;
        }

        float trainDistance = train.distanceAlongRoute;
        if (previousFrontPosition == float.MinValue)
        {
            previousFrontPosition = trainDistance;
        }

        bool hasRedAhead = routeData.TryGetNextRedSignal(trainDistance, out float redSignalDistance);
        bool hasYellowAhead = routeData.TryGetNextYellowSignal(trainDistance, out float yellowSignalDistance);

        if ((!hasRedAhead || trainInRedZone) && activeRedSignalX >= 0f && !redSignalCleared && !hasFailedSignal)
        {
            redSignalDistance = activeRedSignalX;
            hasRedAhead = true;
        }

        if (!hasRedAhead)
        {
            if (infoText != null)
            {
                infoText.text = "GREEN - End of route";
            }

            previousFrontPosition = trainDistance;
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

        if (atRedSignal && trainStopped)
        {
            trainStoppedInsideRedZone = true;
        }

        string message;

        if (redSignalCleared)
        {
            activeSignalState = ActiveSignalState.Green;
            message = "GREEN - Proceed";
        }
        else if (!hasYellowAhead || trainDistance < yellowSignalDistance)
        {
            activeSignalState = ActiveSignalState.Green;
            message = "GREEN";
        }
        else if (trainDistance < redSignalDistance)
        {
            activeSignalState = ActiveSignalState.Yellow;
            message = "YELLOW - Prepare to stop";
        }
        else
        {
            activeSignalState = ActiveSignalState.Danger;
            message = "RED - STOP";
        }

        if (!hasFailedSignal && !redSignalCleared && atRedSignal && trainStopped)
        {
            stoppedTimer += Time.deltaTime;
            message = "Stopped at red signal - wait...";

            if (stoppedTimer >= waitTimeBeforeGreen)
            {
                redSignalCleared = true;
                SetRedSignalToGreen(redSignalDistance);
                message = "Signal cleared - proceed";
            }
        }
        else if (!atRedSignal || !trainStopped)
        {
            stoppedTimer = 0f;
        }

        bool crossedActiveRed = previousFrontPosition <= redSignalDistance && trainDistance > redSignalDistance;

        if (!hasFailedSignal && !redSignalCleared && crossedActiveRed && activeSignalState == ActiveSignalState.Danger)
        {
            hasFailedSignal = true;
            message = "SPAD! You passed a red signal!";

            if (SessionRunStats.Instance != null)
            {
                SessionRunStats.Instance.RecordSpadViolation(trainDistance);
            }
        }
        else if (!hasFailedSignal && !redSignalCleared && passedRed && train.speed > stoppedSpeed)
        {
            hasFailedSignal = true;
            message = "SPAD! You passed a red signal!";

            if (SessionRunStats.Instance != null)
            {
                SessionRunStats.Instance.RecordSpadViolation(trainDistance);
            }
        }

        if (infoText != null)
        {
            infoText.text = message;
        }

        previousFrontPosition = trainDistance;
    }

    private void SetRedSignalToGreen(float redSignalDistance)
    {
        if (redSignalVisuals.TryGetValue(redSignalDistance, out SignalVisual redVisual) && redVisual != null)
        {
            redVisual.SetSignal(greenMat, greenHex);
        }
    }
}
