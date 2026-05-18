using System.Collections;
using UnityEngine;

public class StationPassengerTrigger : MonoBehaviour
{
    [Header("References")]
    public TrainController train;
    public TrainPassengerManager passengerManager;
    public GameObject passengerPrefab;

    [Header("Boarding (Platform -> Train)")]
    public Transform[] platformSpawnPoints;
    public Transform trainBoardTargetPoint;
    public int maxBoardingAttempt = 12;

    [Header("Exiting (Train -> Platform)")]
    public Transform trainExitSpawnPoint;
    public Transform platformExitMidPoint;
    public Transform platformExitFinalPoint;
    public int maxExitingAttempt = 8;
    public float exitMinTurnAngle = 70f;
    public float exitMaxTurnAngle = 160f;
    public float exitWanderDistance = 20f;
    public float exitWanderSeconds = 10f;

    [Header("Timing")]
    public float spawnDelay = 0.3f;

    [Header("Stop Check")]
    public float stoppedSpeed = 0.5f;
    public float dwellDoorOpenGraceSeconds = 3f;

    private bool trainInsideStation = false;
    private bool transferStarted = false;
    private bool trainStoppedInsideStation = false;
    private bool dwellViolationRecordedForThisStop = false;
    private bool validStopCompletedThisStation = false;
    private float dwellDoorClosedTimer = 0f;

    private void Awake()
    {
        if (train == null)
        {
            train = FindFirstObjectByType<TrainController>();
        }

        if (passengerManager == null)
        {
            passengerManager = FindFirstObjectByType<TrainPassengerManager>();
        }
    }

    private void Update()
    {
        if (trainInsideStation && train != null)
        {
            bool trainStopped = train.speed <= stoppedSpeed;

            if (trainStopped)
            {
                trainStoppedInsideStation = true;

                if (!train.IsDoorOpen && !dwellViolationRecordedForThisStop)
                {
                    dwellDoorClosedTimer += Time.deltaTime;
                    if (dwellDoorClosedTimer >= dwellDoorOpenGraceSeconds)
                    {
                        dwellViolationRecordedForThisStop = true;
                        if (SessionRunStats.Instance != null)
                        {
                            SessionRunStats.Instance.RecordDwellTimeViolation();
                        }
                    }
                }
                else if (train.IsDoorOpen)
                {
                    dwellDoorClosedTimer = 0f;
                }
            }
        }

        if (transferStarted)
        {
            return;
        }

        if (CanTransferPassengersNow())
        {
            transferStarted = true;
            validStopCompletedThisStation = true;
            StartCoroutine(HandlePassengerTransfer());
        }
    }

    private bool CanTransferPassengersNow()
    {
        return trainInsideStation
            && train != null
            && train.speed <= stoppedSpeed
            && train.IsDoorOpen;
    }

    private IEnumerator HandlePassengerTransfer()
    {
        int exitCount = 0;
        int boardCount = 0;

        if (passengerManager != null)
        {
            if (passengerManager.currentPassengers > 0)
            {
                int randomExitRequest = Random.Range(1, maxExitingAttempt + 1);
                exitCount = passengerManager.RemovePassengers(randomExitRequest);
                if (SessionRunStats.Instance != null)
                {
                    SessionRunStats.Instance.RecordPassengerDropOff(exitCount);
                }
            }
            yield return StartCoroutine(SpawnExitingPassengers(exitCount));

            boardCount = passengerManager.AvailableSpace > 0
                ? Mathf.Min(maxBoardingAttempt, passengerManager.AvailableSpace)
                : 0;

            yield return StartCoroutine(SpawnBoardingPassengers(boardCount));
        }
        else
        {
            yield return StartCoroutine(SpawnExitingPassengers(maxExitingAttempt));
            yield return StartCoroutine(SpawnBoardingPassengers(maxBoardingAttempt));
        }
    }

    private IEnumerator SpawnExitingPassengers(int count)
    {
        if (passengerPrefab == null || trainExitSpawnPoint == null || platformExitMidPoint == null || platformExitFinalPoint == null)
        {
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            yield return new WaitUntil(CanTransferPassengersNow);
            SpawnExitingPassengerTwoStage();
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private IEnumerator SpawnBoardingPassengers(int count)
    {
        if (passengerPrefab == null || trainBoardTargetPoint == null || platformSpawnPoints == null || platformSpawnPoints.Length == 0)
        {
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            yield return new WaitUntil(CanTransferPassengersNow);
            Transform spawnPoint = platformSpawnPoints[i % platformSpawnPoints.Length];
            SpawnPassenger(spawnPoint, trainBoardTargetPoint.position, PassengerWalker.PassengerFlow.Boarding);
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void SpawnPassenger(Transform spawnPoint, Vector3 target, PassengerWalker.PassengerFlow flow)
    {
        GameObject passenger = Instantiate(passengerPrefab, spawnPoint.position, spawnPoint.rotation);
        PassengerWalker walker = passenger.GetComponent<PassengerWalker>();

        if (walker != null)
        {
            walker.Setup(target, flow, OnPassengerReachedTarget);
        }
    }

    private void SpawnExitingPassengerTwoStage()
    {
        GameObject passenger = Instantiate(passengerPrefab, trainExitSpawnPoint.position, trainExitSpawnPoint.rotation);
        PassengerWalker walker = passenger.GetComponent<PassengerWalker>();

        if (walker != null)
        {
            walker.SetupExitingTwoStage(
                platformExitMidPoint.position,
                platformExitFinalPoint.position,
                exitMinTurnAngle,
                exitMaxTurnAngle,
                exitWanderDistance,
                exitWanderSeconds,
                OnPassengerReachedTarget
            );
        }
    }

    private void OnPassengerReachedTarget(PassengerWalker.PassengerFlow flow)
    {
        if (passengerManager == null)
        {
            return;
        }

        if (flow == PassengerWalker.PassengerFlow.Boarding)
        {
            int boarded = passengerManager.AddPassengers(1);
            if (SessionRunStats.Instance != null)
            {
                SessionRunStats.Instance.RecordPassengerPickup(boarded);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<TrainController>() != null)
        {
            trainInsideStation = true;
            transferStarted = false;
            trainStoppedInsideStation = false;
            validStopCompletedThisStation = false;
            dwellViolationRecordedForThisStop = false;
            dwellDoorClosedTimer = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<TrainController>() != null)
        {
            trainInsideStation = false;

            if (trainStoppedInsideStation && !validStopCompletedThisStation && SessionRunStats.Instance != null)
            {
                SessionRunStats.Instance.RecordMissedStop();
            }

            if (SessionRunStats.Instance != null)
            {
                SessionRunStats.Instance.PrintRunSummary();
            }
        }
    }
}
