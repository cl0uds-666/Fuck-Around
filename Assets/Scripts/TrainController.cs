using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TrainController : MonoBehaviour
{
    [Header("Input System")]
    public InputActionAsset inputActions;

    private InputAction throttleAction;
    private InputAction brakeAction;
    private InputAction throttleUpAction;
    private InputAction throttleDownAction;
    private InputAction brakeUpAction;
    private InputAction brakeDownAction;

    [Header("Controller Calibration")]
    public float inputDeadzone = 0.08f;
    public float inputSmoothSpeed = 5f;

    [Header("Route")]
    public float distanceAlongRoute = 0f;

    [Header("Speed")]
    public float speed = 0f;
    public float maxSpeed = 40f;
    public float previousSpeed;

    [Header("Throttle")]
    [Range(0f, 1f)]
    public float throttle = 0f;
    public float throttleChangeSpeed = 0.5f;
    public float accelerationPower = 2f;

    [Header("Brake")]
    [Range(0f, 1f)]
    public float brake = 0f;
    public float brakeChangeSpeed = 0.7f;
    public float brakePower = 4f;

    [Header("Coasting / Resistance")]
    public float baseCoastDeceleration = 0.15f;
    public int carriageCount = 1;
    public float carriageResistance = 0.03f;

    [Header("UI")]
    public Slider throttleSlider;
    public Slider brakeSlider;

    [Header("Audio")]
    public AudioSource engineSource;
    

    public float minPitch = 0.7f;
    public float maxPitch = 1.5f;

    public float minVolume = 0.2f;
    public float maxVolume = 1f;

    [Header("Brake Audio")]
    public AudioSource brakeSource;
    public AudioSource squealSource;
    public AudioSource brakeReleaseSource;

    public float brakeFadeSpeedThreshold = 8f;
    public float squealSpeedThreshold = 5f;
    public float stoppedThreshold = 0.1f;

    private bool releasePlayedThisStop = false;
    private bool isFadingRelease = false;

    [Header("Ambience")]
    public AudioSource ambienceSource;

    [Range(0f, 1f)]
    public float ambienceVolume = 0.4f;

    private float previousBrake;


    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("Input Actions asset is not assigned on TrainController.");
            return;
        }

        InputActionMap trainMap = inputActions.FindActionMap("TrainAction");

        if (trainMap == null)
        {
            Debug.LogError("Could not find Action Map called 'Train'. Check spelling.");
            return;
        }

        throttleAction = trainMap.FindAction("Throttle");
        brakeAction = trainMap.FindAction("Brake");
        throttleUpAction = trainMap.FindAction("ThrottleUp");
        throttleDownAction = trainMap.FindAction("ThrottleDown");
        brakeUpAction = trainMap.FindAction("BrakeUp");
        brakeDownAction = trainMap.FindAction("BrakeDown");

        if (throttleAction == null) Debug.LogError("Missing action: Throttle");
        if (brakeAction == null) Debug.LogError("Missing action: Brake");
        if (throttleUpAction == null) Debug.LogError("Missing action: ThrottleUp");
        if (throttleDownAction == null) Debug.LogError("Missing action: ThrottleDown");
        if (brakeUpAction == null) Debug.LogError("Missing action: BrakeUp");
        if (brakeDownAction == null) Debug.LogError("Missing action: BrakeDown");
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void Update()
    {
        previousSpeed = speed;

        HandleInput();
        ApplyMovement();
        UpdateUI();

        UpdateEngineAudio();
        UpdateBrakeAudio();

        UpdateAmbience();
    }

    private void HandleInput()
    {
        // Controller levers
        float rawThrottle = throttleAction.ReadValue<float>();
        float rawBrake = brakeAction.ReadValue<float>();

        float controllerThrottle = Mathf.InverseLerp(-1f, 1f, rawThrottle);
        float controllerBrake = Mathf.InverseLerp(-1f, 1f, rawBrake);

        controllerThrottle = ApplyDeadzone(controllerThrottle);
        controllerBrake = ApplyDeadzone(controllerBrake);

        // Keyboard notch controls
        if (throttleUpAction.IsPressed())
        {
            throttle += throttleChangeSpeed * Time.deltaTime;
        }

        if (throttleDownAction.IsPressed())
        {
            throttle -= throttleChangeSpeed * Time.deltaTime;
        }

        if (brakeUpAction.IsPressed())
        {
            brake += brakeChangeSpeed * Time.deltaTime;
        }

        if (brakeDownAction.IsPressed())
        {
            brake -= brakeChangeSpeed * Time.deltaTime;
        }

        bool keyboardUsed =
            throttleUpAction.IsPressed() ||
            throttleDownAction.IsPressed() ||
            brakeUpAction.IsPressed() ||
            brakeDownAction.IsPressed();

        if (!keyboardUsed)
        {
            throttle = Mathf.MoveTowards(throttle, controllerThrottle, inputSmoothSpeed * Time.deltaTime);
            brake = Mathf.MoveTowards(brake, controllerBrake, inputSmoothSpeed * Time.deltaTime);
        }

        throttle = Mathf.Clamp01(throttle);
        brake = Mathf.Clamp01(brake);
    }

    private float ApplyDeadzone(float value)
    {
        if (value < inputDeadzone)
        {
            return 0f;
        }

        if (value > 1f - inputDeadzone)
        {
            return 1f;
        }

        return value;
    }

    private void ApplyMovement()
    {
        float acceleration = throttle * accelerationPower;
        float braking = brake * brakePower;

        speed += acceleration * Time.deltaTime;
        speed -= braking * Time.deltaTime;

        float coastResistance = baseCoastDeceleration + (carriageCount * carriageResistance);

        if (throttle <= 0.01f && brake <= 0.01f && speed > 0f)
        {
            speed -= coastResistance * Time.deltaTime;
        }

        speed = Mathf.Clamp(speed, 0f, maxSpeed);

        distanceAlongRoute += speed * Time.deltaTime;

        transform.position = new Vector3(
            distanceAlongRoute,
            transform.position.y,
            transform.position.z
        );
    }

    void UpdateEngineAudio()
    {
        if (engineSource == null) return;

        float speedPercent = speed / maxSpeed;

        if (speed <= 0.1f)
        {
            engineSource.Stop();
            return;
        }

        engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedPercent);
        engineSource.volume = Mathf.Lerp(minVolume, maxVolume, speedPercent);

        if (!engineSource.isPlaying)
        {
            engineSource.loop = true;
            engineSource.Play();
        }
    }

    void UpdateBrakeAudio()
    {
        bool trainMoving = speed > stoppedThreshold;
        bool brakeApplied = brake > 0.05f;

        // Normal brake loop
        if (brakeSource != null)
        {
            if (trainMoving && brakeApplied)
            {
                if (!brakeSource.isPlaying)
                {
                    brakeSource.loop = true;
                    brakeSource.Play();
                }

                float speedFade = Mathf.Clamp01(speed / brakeFadeSpeedThreshold);
                brakeSource.volume = brake * speedFade;
            }
            else
            {
                brakeSource.volume = Mathf.MoveTowards(
                    brakeSource.volume,
                    0f,
                    2f * Time.deltaTime
                );

                if (brakeSource.volume <= 0.01f && brakeSource.isPlaying)
                {
                    brakeSource.Stop();
                }
            }
        }

        // Low-speed squeal
        if (squealSource != null)
        {
            bool shouldSqueal = trainMoving && brakeApplied && speed <= squealSpeedThreshold;

            if (shouldSqueal)
            {
                if (!squealSource.isPlaying)
                {
                    squealSource.loop = true;
                    squealSource.Play();
                }

                float squealVolume = Mathf.Clamp01(1f - (speed / squealSpeedThreshold));
                squealSource.volume = squealVolume * brake;
            }
            else
            {
                squealSource.volume = Mathf.MoveTowards(
                    squealSource.volume,
                    0f,
                    3f * Time.deltaTime
                );

                if (squealSource.volume <= 0.01f && squealSource.isPlaying)
                {
                    squealSource.Stop();
                }
            }
        }

        // Pressure release when fully stopped
        if (!trainMoving && brakeApplied && !releasePlayedThisStop)
        {
            if (brakeReleaseSource != null)
            {
                brakeReleaseSource.volume = 1f;
                brakeReleaseSource.loop = false;
                brakeReleaseSource.Play();
                isFadingRelease = true;
            }

            releasePlayedThisStop = true;
        }

        if (isFadingRelease && brakeReleaseSource != null)
        {
            brakeReleaseSource.volume = Mathf.MoveTowards(
                brakeReleaseSource.volume,
                0f,
                0.5f * Time.deltaTime
            );

            if (brakeReleaseSource.volume <= 0.01f)
            {
                brakeReleaseSource.Stop();
                isFadingRelease = false;
            }
        }

        // Reset release trigger once moving again
        if (trainMoving)
        {
            releasePlayedThisStop = false;
        }

        previousBrake = brake;
    }



    void UpdateAmbience()
    {
        if (ambienceSource == null) return;

        if (!ambienceSource.isPlaying)
        {
            ambienceSource.loop = true;
            ambienceSource.Play();
        }

        ambienceSource.volume = ambienceVolume;
    }

    private void UpdateUI()
    {
        if (throttleSlider != null)
        {
            throttleSlider.value = throttle;
        }

        if (brakeSlider != null)
        {
            brakeSlider.value = brake;
        }
    }
}