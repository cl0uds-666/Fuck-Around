using UnityEngine;

namespace SplineArchitect.Examples
{
    [ExecuteAlways]
    public class SAVehicleAndTrailer : SAVehicle
    {
        public Transform trailer;
        public Transform trailerAnchor;

        private new void Start()
        {
            base.Start();
        }

        private new void Update()
        {
            base.Update();

#if UNITY_EDITOR
            SplineObject so = GetComponent<SplineObject>();
            if (so != null && transform != null)
            {
#endif
                trailer.rotation = Quaternion.LookRotation(trailerAnchor.position - trailer.position, Vector3.up);

#if UNITY_EDITOR
            }
#endif
        }
    }
}
