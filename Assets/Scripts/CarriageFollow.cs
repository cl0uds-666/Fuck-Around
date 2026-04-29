using UnityEngine;

public class CarriageFollow : MonoBehaviour
{
    [Header("References")]
    public TrainController train;

    [Header("Offsets")]
    public float normalOffset = -10.5f;
    public float maxBackOffset = -11f;

    [Header("Speeds")]
    public float extendSpeed = 1.5f;   // how fast it stretches when accelerating
    public float returnSpeed = 2.5f;   // how fast it compresses when braking

    private float currentOffset;
    private float previousSpeed;

    private void Start()
    {
        currentOffset = normalOffset;
        previousSpeed = train.speed;

        // Start aligned
        transform.position = new Vector3(
            train.distanceAlongRoute + currentOffset,
            train.transform.position.y,
            train.transform.position.z
        );
    }

    private void LateUpdate()
    {
        if (train == null) return;

        float acceleration = train.speed - previousSpeed;

        // Accelerating go towards -11
        if (acceleration > 0.01f)
        {
            currentOffset = Mathf.MoveTowards(
                currentOffset,
                maxBackOffset,
                extendSpeed * Time.deltaTime
            );
        }
        // Braking  go towards -10.5
        else if (acceleration < -0.01f)
        {
            currentOffset = Mathf.MoveTowards(
                currentOffset,
                normalOffset,
                returnSpeed * Time.deltaTime
            );
        }

        // Hard clamp (never break bounds)
        currentOffset = Mathf.Clamp(currentOffset, maxBackOffset, normalOffset);

        float targetX = train.distanceAlongRoute + currentOffset;

        transform.position = new Vector3(
            targetX,
            train.transform.position.y,
            train.transform.position.z
        );

        previousSpeed = train.speed;
    }
}