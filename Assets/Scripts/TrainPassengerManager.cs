using UnityEngine;

public class TrainPassengerManager : MonoBehaviour
{
    [Header("Capacity")]
    public int maxPassengers = 120;
    public int currentPassengers = 40;

    public int AvailableSpace
    {
        get { return Mathf.Max(0, maxPassengers - currentPassengers); }
    }

    public int RemovePassengers(int requested)
    {
        int removed = Mathf.Clamp(requested, 0, currentPassengers);
        currentPassengers -= removed;
        return removed;
    }

    public int AddPassengers(int requested)
    {
        int boarded = Mathf.Clamp(requested, 0, AvailableSpace);
        currentPassengers += boarded;
        return boarded;
    }
}
