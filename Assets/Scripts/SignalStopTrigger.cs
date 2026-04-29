using UnityEngine;

public class SignalStopTrigger : MonoBehaviour
{
    public SignalManager signalManager;
    public bool isRedSignalTrigger = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!isRedSignalTrigger) return;

        TrainController train = other.GetComponentInParent<TrainController>();

        if (train != null && signalManager != null)
        {
            signalManager.SetTrainInRedZone(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isRedSignalTrigger) return;

        TrainController train = other.GetComponentInParent<TrainController>();

        if (train != null && signalManager != null)
        {
            signalManager.SetTrainInRedZone(false);
        }
    }
}