using System.Collections;
using UnityEngine;

public class StationPassengerTrigger : MonoBehaviour
{
    [Header("References")]
    public TrainController train;
    public GameObject passengerPrefab;

    [Header("Passenger Spawn")]
    public Transform[] passengerSpawnPoints;
    public Transform passengerTargetPoint;
    public int passengerCount = 6;
    public float spawnDelay = 0.3f;

    [Header("Stop Check")]
    public float stoppedSpeed = 0.5f;

    private bool trainInsideStation = false;
    private bool passengersSpawned = false;

    private void Awake()
    {
        if (train == null)
        {
            train = FindFirstObjectByType<TrainController>();
        }
    }

    private void Update()
    {
        if (passengersSpawned)
        {
            return;
        }

        if (trainInsideStation && train.speed <= stoppedSpeed)
        {
            passengersSpawned = true;
            StartCoroutine(SpawnPassengers());
        }
    }

    private IEnumerator SpawnPassengers()
    {
        for (int i = 0; i < passengerCount; i++)
        {
            Transform spawnPoint = passengerSpawnPoints[i % passengerSpawnPoints.Length];

            GameObject passenger = Instantiate(
                passengerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            PassengerWalker walker = passenger.GetComponent<PassengerWalker>();

            if (walker != null && passengerTargetPoint != null)
            {
                walker.SetTarget(passengerTargetPoint.position);
            }

            yield return new WaitForSeconds(spawnDelay);
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