using UnityEngine;

namespace SplineArchitect.Examples
{
    public class SAFollowSpline : MonoBehaviour
    {
        [Header("Settings")]
        public float speed;

        private SplineObject splineObject;

        void Start()
        {
            splineObject = GetComponent<SplineObject>();
        }

        void Update()
        {
            splineObject.localSplinePosition.z += Time.deltaTime * speed;
        }
    }
}
