using UnityEngine;

public class StationSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject stationPrefab;
    public RouteData routeData;

    [Header("Placement")]
    public float zOffset = -6f;
    public float yOffset = 2.7f;
    public float yRotation = 0f;

    private void Start()
    {
        if (routeData == null || stationPrefab == null)
        {
            return;
        }

        foreach (float stationX in routeData.stationPositions)
        {
            SpawnStation(stationX);
        }
    }

    private void SpawnStation(float xPosition)
    {
        Vector3 position = new Vector3(xPosition, yOffset, zOffset);
        Quaternion rotation = Quaternion.Euler(0f, yRotation, 0f);

        GameObject station = Instantiate(stationPrefab, position, rotation, transform);
        station.name = "Station";
    }
}
