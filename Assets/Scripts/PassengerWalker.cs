using System;
using UnityEngine;

public class PassengerWalker : MonoBehaviour
{
    public enum PassengerFlow
    {
        Boarding,
        Exiting
    }

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float despawnDistance = 1f;

    [Header("Arm Swing")]
    public Transform leftArm;
    public Transform rightArm;
    public float swingAngle = 40f;
    public float swingSpeed = 5f;

    private Vector3 targetPosition;
    private Action<PassengerFlow> onReachedTarget;
    private PassengerFlow flowType;
    private bool hasSecondStageTarget = false;
    private Vector3 secondStageTarget;
    private bool inSecondStage = false;
    private bool isWandering = false;
    private float wanderTimer = 0f;
    private float wanderDuration = 10f;
    private float wanderMoveDistance = 20f;

    public void Setup(Vector3 target, PassengerFlow flow, Action<PassengerFlow> reachedCallback)
    {
        targetPosition = target;
        flowType = flow;
        onReachedTarget = reachedCallback;
        hasSecondStageTarget = false;
        inSecondStage = false;
        isWandering = false;
        wanderTimer = 0f;
    }

    public void SetupExitingTwoStage(
        Vector3 firstTarget,
        Vector3 secondTarget,
        float minTurnAngle,
        float maxTurnAngle,
        float wanderDistance,
        float wanderSeconds,
        Action<PassengerFlow> reachedCallback)
    {
        targetPosition = firstTarget;
        secondStageTarget = secondTarget;
        flowType = PassengerFlow.Exiting;
        onReachedTarget = reachedCallback;
        hasSecondStageTarget = true;
        inSecondStage = false;
        isWandering = false;
        wanderMoveDistance = Mathf.Max(1f, wanderDistance);
        wanderDuration = Mathf.Max(0.1f, wanderSeconds);
        wanderTimer = 0f;
        this.minTurnAngle = Mathf.Clamp(minTurnAngle, 0f, 180f);
        this.maxTurnAngle = Mathf.Clamp(maxTurnAngle, this.minTurnAngle, 180f);
    }
    private float minTurnAngle = 70f;
    private float maxTurnAngle = 160f;

    private void Update()
    {
        MovePassenger();
        SwingArms();
    }

    private void MovePassenger()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        Vector3 direction = targetPosition - transform.position;

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        if (Vector3.Distance(transform.position, targetPosition) <= despawnDistance)
        {
            if (hasSecondStageTarget && !inSecondStage)
            {
                targetPosition = secondStageTarget;
                inSecondStage = true;
                return;
            }

            if (hasSecondStageTarget && inSecondStage && !isWandering)
            {
                BeginWander();
                return;
            }

            if (!hasSecondStageTarget && onReachedTarget != null)
            {
                onReachedTarget(flowType);
            }

            Destroy(gameObject);
        }

        if (isWandering)
        {
            wanderTimer += Time.deltaTime;

            if (wanderTimer >= wanderDuration)
            {
                if (onReachedTarget != null)
                {
                    onReachedTarget(flowType);
                }

                Destroy(gameObject);
            }
        }
    }

    private void BeginWander()
    {
        isWandering = true;
        wanderTimer = 0f;

        float turnAngle = Random.Range(minTurnAngle, maxTurnAngle);
        if (Random.value < 0.5f)
        {
            turnAngle *= -1f;
        }

        Vector3 turnedDirection = Quaternion.AngleAxis(turnAngle, Vector3.up) * transform.forward;
        turnedDirection.y = 0f;
        turnedDirection.Normalize();

        if (turnedDirection.sqrMagnitude < 0.001f)
        {
            turnedDirection = transform.right;
        }

        targetPosition = transform.position + turnedDirection * wanderMoveDistance;
    }

    private void SwingArms()
    {
        float swing = Mathf.Sin(Time.time * swingSpeed) * swingAngle;

        if (leftArm != null)
        {
            leftArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
        }

        if (rightArm != null)
        {
            rightArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        }
    }
}
