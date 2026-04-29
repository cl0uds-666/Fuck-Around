using UnityEngine;

namespace VFXTools
{
    [ExecuteAlways]
    public class VFXController : MonoBehaviour
    {
        [Header("Rain Enabled")]
        [SerializeField] private bool rainEnabled = true;

        [Header("Adjustable Parameters")]
        [SerializeField] private Color particleColor = Color.white;
        [SerializeField, Range(0f, 4f)] private float intensity = 1f;
        [SerializeField] private Vector3 windDirection = Vector3.zero;

        [Header("Follow Target")]
        [SerializeField] private Transform targetToFollow;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 15f, 0f);
        [SerializeField] private bool followRotation = true;

        private ParticleSystem[] particleSystems;
        private float[] defaultRateOverTimeValues;

        private void Awake()
        {
            CacheParticles();
            ApplyVisualSettings();
            ApplyEnabledState();
        }

        private void OnValidate()
        {
            CacheParticles();
            ApplyVisualSettings();
            ApplyEnabledState();
        }

        private void LateUpdate()
        {
            FollowTarget();

            if (Application.isPlaying)
            {
                ApplyEnabledState();
            }
        }

        private void CacheParticles()
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);

            if (defaultRateOverTimeValues == null || defaultRateOverTimeValues.Length != particleSystems.Length)
            {
                defaultRateOverTimeValues = new float[particleSystems.Length];
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] == null) continue;

                var emission = particleSystems[i].emission;

                if (defaultRateOverTimeValues[i] <= 0f)
                {
                    defaultRateOverTimeValues[i] = emission.rateOverTime.constant;
                }
            }
        }

        private void FollowTarget()
        {
            if (targetToFollow == null)
            {
                return;
            }

            transform.position = targetToFollow.position + followOffset;

            if (followRotation)
            {
                transform.rotation = targetToFollow.rotation;
            }
        }

        private void ApplyEnabledState()
        {
            if (particleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps == null) continue;

                var emission = ps.emission;
                emission.enabled = rainEnabled;
            }
        }

        private void ApplyVisualSettings()
        {
            if (particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];

                if (ps == null) continue;

                var main = ps.main;
                var emission = ps.emission;
                var velocityOverLifetime = ps.velocityOverLifetime;

                main.startColor = particleColor;

                var rate = emission.rateOverTime;
                rate.constant = defaultRateOverTimeValues[i] * intensity;
                emission.rateOverTime = rate;

                if (velocityOverLifetime.enabled)
                {
                    velocityOverLifetime.x = windDirection.x;
                    velocityOverLifetime.y = windDirection.y;
                    velocityOverLifetime.z = windDirection.z;
                }
            }
        }

        public void SetRainEnabled(bool enabled)
        {
            rainEnabled = enabled;
            ApplyEnabledState();
        }

        public void SetParticleColor(Color newColor)
        {
            particleColor = newColor;
            ApplyVisualSettings();
        }

        public void SetIntensity(float newIntensity)
        {
            intensity = Mathf.Clamp(newIntensity, 0f, 4f);
            ApplyVisualSettings();
        }

        public void SetWindDirection(Vector3 newWindDirection)
        {
            windDirection = newWindDirection;
            ApplyVisualSettings();
        }

        public bool GetRainEnabled()
        {
            return rainEnabled;
        }

        public Color GetParticleColor()
        {
            return particleColor;
        }

        public float GetIntensity()
        {
            return intensity;
        }

        public Vector3 GetWindDirection()
        {
            return windDirection;
        }
    }
}

//using UnityEngine;

//namespace FullOpaqueRain
//{
//    [ExecuteAlways]
//    [DisallowMultipleComponent]
//    public class VFXController : MonoBehaviour
//    {
//        [Header("Paramètres Modifiables")]
//        [SerializeField, Tooltip("Couleur des particules générées.")]
//        private Color particleColor = Color.white;

//        [SerializeField, Min(0f), Tooltip("Taux d'émission des particules (Rate over Time). Par défaut : 200.")]
//        private float intensity = 200f;

//        [SerializeField, Tooltip("Direction et force du vent appliquée aux particules.")]
//        private Vector3 windDirection = Vector3.zero;

//        [SerializeField, Range(0f, 10f), Tooltip("Puissance globale du vent appliquée à la direction.")]
//        private float windStrength = 1f;

//        private ParticleSystem[] particleSystems;

//        // Cache pour éviter les mises à jour inutiles
//        private Color lastColor;
//        private float lastIntensity;
//        private Vector3 lastWindDirection;
//        private float lastWindStrength;

//        // =====================
//        // == Cycle de vie ==
//        // =====================

//        private void Awake()
//        {
//            ApplySettings();
//        }

//        private void OnValidate()
//        {
//            // Évite les réapplications en boucle pendant le Play mode
//            if (!Application.isPlaying)
//                ApplySettings();
//        }

//        // =====================
//        // == Méthodes internes ==
//        // =====================

//        private void EnsureParticlesCached()
//        {
//            if (particleSystems == null || particleSystems.Length == 0)
//            {
//                particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
//            }
//        }

//        private void ApplySettings()
//        {
//            EnsureParticlesCached();

//            // Empêche le recalcul si rien n’a changé
//            if (particleColor == lastColor &&
//                Mathf.Approximately(intensity, lastIntensity) &&
//                windDirection == lastWindDirection &&
//                Mathf.Approximately(windStrength, lastWindStrength))
//                return;

//            // Met à jour les caches
//            lastColor = particleColor;
//            lastIntensity = intensity;
//            lastWindDirection = windDirection;
//            lastWindStrength = windStrength;

//            // Applique les paramètres à tous les systèmes
//            foreach (var ps in particleSystems)
//            {
//                if (ps == null) continue;

//                var main = ps.main;
//                var emission = ps.emission;
//                var velocityOverLifetime = ps.velocityOverLifetime;

//                // Couleur des particules
//                main.startColor = particleColor;

//                // Taux d'émission
//                var rate = emission.rateOverTime;
//                rate.constant = intensity;
//                emission.rateOverTime = rate;

//                // Direction du vent (si activé)
//                if (velocityOverLifetime.enabled)
//                {
//                    velocityOverLifetime.x = windDirection.x * windStrength;
//                    velocityOverLifetime.y = windDirection.y * windStrength;
//                    velocityOverLifetime.z = windDirection.z * windStrength;
//                }
//            }
//        }

//        // =====================
//        // == Méthodes publiques ==
//        // =====================

//        public void SetParticleColor(Color newColor)
//        {
//            particleColor = newColor;
//            ApplySettings();
//        }

//        public void SetIntensity(float newIntensity)
//        {
//            intensity = Mathf.Max(0f, newIntensity);
//            ApplySettings();
//        }

//        public void SetWindDirection(Vector3 newWindDirection)
//        {
//            windDirection = newWindDirection;
//            ApplySettings();
//        }

//        public void SetWindStrength(float newStrength)
//        {
//            windStrength = Mathf.Max(0f, newStrength);
//            ApplySettings();
//        }

//        // === Getters ===
//        public Color GetParticleColor() => particleColor;
//        public float GetIntensity() => intensity;
//        public Vector3 GetWindDirection() => windDirection;
//        public float GetWindStrength() => windStrength;
//    }
//}
