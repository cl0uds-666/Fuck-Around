using UnityEngine;

public class SignalSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject signalPrefab;
    public RouteData routeData;
    public SignalManager signalManager;

    [Header("Placement")]
    public float zOffset = 5f;
    public float yRotation = -90f;

    [Header("Materials")]
    public Material greenMat;
    public Material yellowMat;
    public Material redMat;

    [Header("Light Colours")]
    public string greenHex = "#00FF00";
    public string yellowHex = "#FFFF00";
    public string redHex = "#FF0000";

    private void Start()
    {
        if (routeData == null)
        {
            return;
        }

        foreach (float greenX in routeData.greenSignalPositions)
        {
            SpawnSignal(greenX, greenMat, greenHex, "Green Signal", false);
        }

        foreach (float yellowX in routeData.yellowSignalPositions)
        {
            SpawnSignal(yellowX, yellowMat, yellowHex, "Yellow Signal", false);
        }

        foreach (float redX in routeData.redSignalPositions)
        {
            SpawnSignal(redX, redMat, redHex, "Red Signal", true);
        }
    }

    private void SpawnSignal(float xPosition, Material material, string hexColour, string signalName, bool isRed)
    {
        Vector3 position = new Vector3(xPosition, transform.position.y, zOffset);
        Quaternion rotation = Quaternion.Euler(0f, yRotation, 0f);

        GameObject signal = Instantiate(signalPrefab, position, rotation, transform);
        signal.name = signalName;

        SignalVisual visual = signal.GetComponent<SignalVisual>();

        if (visual != null)
        {
            visual.SetSignal(material, hexColour);
        }

        if (isRed && signalManager != null)
        {
            signalManager.RegisterRedSignalVisual(xPosition, visual);
        }

        SignalStopTrigger stopTrigger = signal.GetComponentInChildren<SignalStopTrigger>();

        if (stopTrigger != null)
        {
            stopTrigger.signalManager = signalManager;
            stopTrigger.isRedSignalTrigger = isRed;
        }
    }
}
