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

    public void Setup(Vector3 target, PassengerFlow flow, Action<PassengerFlow> reachedCallback)
    {
        targetPosition = target;
        flowType = flow;
        onReachedTarget = reachedCallback;
    }

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
            if (onReachedTarget != null)
            {
                onReachedTarget(flowType);
            }

            Destroy(gameObject);
        }
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
