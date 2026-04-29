using UnityEngine;

public class LandscapeFollower : MonoBehaviour
{
    [Header("References")]
    public Transform train;

    [Header("Position")]
    public float yPosition = -0.01f;
    public float zPosition = 0f;

    [Header("Follow")]
    public bool followTrainX = true;

    private void LateUpdate()
    {
        if (train == null)
        {
            return;
        }

        float x = followTrainX ? train.position.x : transform.position.x;

        transform.position = new Vector3(
            x,
            yPosition,
            zPosition
        );
    }
}