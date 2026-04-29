using UnityEngine;

public class SignalSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject signalPrefab;
    public RouteData routeData;
    public SignalManager signalManager;


    //[Header("Signal Positions")]
    //public float greenSignalX = 250f;
    //public float yellowSignalX = 500f;
    //public float redSignalX = 750f;

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
        SpawnSignal(routeData.greenSignalX, greenMat, greenHex, "Green Signal");
        SpawnSignal(routeData.yellowSignalX, yellowMat, yellowHex, "Yellow Signal");
        SpawnSignal(routeData.redSignalX, redMat, redHex, "Red Signal");
    }


    private void SpawnSignal(float xPosition, Material material, string hexColour, string signalName)
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

        if (signalName == "Red Signal" && signalManager != null)
        {
            signalManager.redSignalVisual = visual;
        }

        SignalStopTrigger stopTrigger = signal.GetComponentInChildren<SignalStopTrigger>();

        if (stopTrigger != null)
        {
            stopTrigger.signalManager = signalManager;
            stopTrigger.isRedSignalTrigger = signalName == "Red Signal";
        }
    }
}