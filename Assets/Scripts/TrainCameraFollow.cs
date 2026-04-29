using UnityEngine;

public class TrainCameraFollow : MonoBehaviour
{
    [Header("References")]
    public TrainController train;

    [Header("Base Camera Position")]
    public Vector3 normalLocalPosition = new Vector3(0f, 13f, -25f);

    [Header("Speed-Based Pullback")]
    public float maxSpeedPullBack = 8f;
    public float speedPullBackSmooth = 2f;

    [Header("Extra Kick")]
    public float maxAccelerationKick = 1.5f;
    public float maxBrakingKick = 3f;
    public float accelerationSensitivity = 0.25f;

    private float previousSpeed;

    private void Start()
    {
        if (train != null)
        {
            previousSpeed = train.speed;
        }
    }

    private void LateUpdate()
    {
        if (train == null)
        {
            return;
        }

        float speedPercent = train.speed / train.maxSpeed;

        float speedPullBack = Mathf.Lerp(0f, maxSpeedPullBack, speedPercent);

        float acceleration = (train.speed - previousSpeed) / Time.deltaTime;

        float accelerationKick = 0f;

        if (acceleration > 0f)
        {
            accelerationKick = -Mathf.Clamp(
                acceleration * accelerationSensitivity,
                0f,
                maxAccelerationKick
            );
        }
        else if (acceleration < 0f)
        {
            accelerationKick = Mathf.Clamp(
                -acceleration * accelerationSensitivity,
                0f,
                maxBrakingKick
            );
        }

        Vector3 targetPosition = normalLocalPosition;

        // Main pullback: based on current speed.
        targetPosition.z -= speedPullBack;

        // Extra temporary kick: acceleration/braking.
        targetPosition.z += accelerationKick;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPosition,
            speedPullBackSmooth * Time.deltaTime
        );

        previousSpeed = train.speed;
    }
}