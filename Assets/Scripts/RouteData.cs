using System.Collections.Generic;
using UnityEngine;

public class RouteData : MonoBehaviour
{
    [Header("References")]
    public TrainController train;

    [Header("Route Length")]
    public float routeLength = 10000f;

    [Header("Stations")]
    public float firstStationMinX = 600f;
    public float firstStationMaxX = 1200f;
    public float minStationGap = 600f;
    public float maxStationGap = 1400f;
    public float redBeforeStationDistance = 50f;

    [Header("Green Signals")]
    public float greenSignalSpacing = 250f;

    [Header("Signal Spacing")]
    public float safetyMultiplier = 1.25f;
    public float minimumSignalSpacing = 100f;

    [Header("Generated Route Data")]
    public List<float> stationPositions = new List<float>();
    public List<float> redSignalPositions = new List<float>();
    public List<float> yellowSignalPositions = new List<float>();
    public List<float> greenSignalPositions = new List<float>();

    private float cachedSignalSpacing;

    private void Awake()
    {
        GenerateRouteData();
    }

    private void OnValidate()
    {
        GenerateRouteData();
    }

    public void GenerateRouteData()
    {
        CalculateSignalSpacing();
        GenerateStations();
        GenerateStationSignals();
        GenerateGreenSignals();
    }

    private void CalculateSignalSpacing()
    {
        if (train == null)
        {
            cachedSignalSpacing = minimumSignalSpacing;
            return;
        }

        float stoppingDistance = (train.maxSpeed * train.maxSpeed) / (2f * train.brakePower);
        float signalSpacing = stoppingDistance * safetyMultiplier;
        cachedSignalSpacing = Mathf.Max(signalSpacing, minimumSignalSpacing);
    }

    private void GenerateStations()
    {
        stationPositions.Clear();

        float firstMin = Mathf.Min(firstStationMinX, firstStationMaxX);
        float firstMax = Mathf.Max(firstStationMinX, firstStationMaxX);

        float nextStation = Random.Range(firstMin, firstMax);

        while (nextStation <= routeLength)
        {
            stationPositions.Add(nextStation);
            nextStation += Random.Range(minStationGap, maxStationGap);
        }
    }

    private void GenerateStationSignals()
    {
        redSignalPositions.Clear();
        yellowSignalPositions.Clear();

        foreach (float stationX in stationPositions)
        {
            float redX = stationX - redBeforeStationDistance;
            float yellowX = redX - cachedSignalSpacing;

            redSignalPositions.Add(redX);
            yellowSignalPositions.Add(yellowX);
        }
    }

    private void GenerateGreenSignals()
    {
        greenSignalPositions.Clear();

        if (greenSignalSpacing <= 0f)
        {
            return;
        }

        for (float x = 0f; x <= routeLength; x += greenSignalSpacing)
        {
            greenSignalPositions.Add(x);
        }
    }

    public bool TryGetNextRedSignal(float trainX, out float redX)
    {
        foreach (float signalX in redSignalPositions)
        {
            if (signalX >= trainX)
            {
                redX = signalX;
                return true;
            }
        }

        redX = 0f;
        return false;
    }

    public bool TryGetNextYellowSignal(float trainX, out float yellowX)
    {
        foreach (float signalX in yellowSignalPositions)
        {
            if (signalX >= trainX)
            {
                yellowX = signalX;
                return true;
            }
        }

        yellowX = 0f;
        return false;
    }

    public bool TryGetNextStation(float trainX, out float stationX)
    {
        foreach (float x in stationPositions)
        {
            if (x >= trainX)
            {
                stationX = x;
                return true;
            }
        }

        stationX = 0f;
        return false;
    }
}
