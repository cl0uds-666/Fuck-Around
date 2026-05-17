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

    private bool trainInsideStation = false;
    private bool transferStarted = false;

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
        if (transferStarted)
        {
            return;
        }

        if (trainInsideStation && train != null && train.speed <= stoppedSpeed)
        {
            transferStarted = true;
            StartCoroutine(HandlePassengerTransfer());
        }
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
            passengerManager.AddPassengers(1);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<TrainController>() != null)
        {
            trainInsideStation = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<TrainController>() != null)
        {
            trainInsideStation = false;
        }
    }
}
