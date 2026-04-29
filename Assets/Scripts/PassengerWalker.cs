using UnityEngine;

public class PassengerWalker : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    public float despawnDistance = 1f;

    [Header("Arm Swing")]
    public Transform leftArm;
    public Transform rightArm;
    public float swingAngle = 40f;
    public float swingSpeed = 5f;

    private Vector3 targetPosition;

    public void SetTarget(Vector3 target)
    {
        targetPosition = target;
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