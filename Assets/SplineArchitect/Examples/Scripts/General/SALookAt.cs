using UnityEngine;

namespace SplineArchitect.Examples
{
    public class SALookAt : MonoBehaviour
    {
        public Transform lookAtPoint;

        void LateUpdate()
        {
            transform.LookAt(lookAtPoint);
        }
    }
}
