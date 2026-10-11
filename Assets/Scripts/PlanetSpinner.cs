using UnityEngine;

namespace AdaptiveVR.Environment
{
    public enum PlanetPreset
    {
        Custom,
        Sun,
        Mercury,
        Venus,
        Earth,
        Mars,
        Jupiter,
        Saturn,
        Uranus,
        Neptune,
        Moon
    }

    /// <summary>
    /// Smoothly rotates a planet on its axis and orbits around a central target (e.g., Sun).
    /// Features realistic astronomical presets and optional attachment of your custom Saturn Ring GameObject.
    /// Works both in Play mode and in Edit mode for real-time scene previewing.
    /// </summary>
    [ExecuteAlways]
    public class PlanetSpinner : MonoBehaviour
    {
        [Header("0. Planetary Preset")]
        [Tooltip("Select a predefined planet to automatically populate realistic tilt, spin speed, and orbit speed.")]
        public PlanetPreset preset = PlanetPreset.Custom;

        [Header("1. Axial Self-Rotation")]
        [Tooltip("Speed of self-rotation around planet's own axis in degrees per second. (Positive = Prograde, Negative/Inverted Tilt = Retrograde)")]
        public float spinSpeed = 30.0f;

        [Tooltip("Axial rotation direction / local axis vector.")]
        public Vector3 spinAxis = Vector3.up;

        [Tooltip("Axial tilt angle in degrees relative to orbital plane (e.g., Earth = 23.44°, Saturn = 26.73°, Uranus = 97.77°).")]
        [Range(-180f, 180f)]
        public float axialTilt = 0.0f;

        [Header("2. Orbital Revolution (Optional)")]
        [Tooltip("Enable orbiting around a central target object (e.g., Sun).")]
        public bool enableOrbit = false;

        [Tooltip("Target object to orbit around (e.g., Sun object).")]
        public Transform orbitTarget;

        [Tooltip("World position center point if orbitTarget is null.")]
        public Vector3 orbitPivot = Vector3.zero;

        [Tooltip("Speed of orbital revolution around central target in degrees per second.")]
        public float orbitSpeed = 10.0f;

        [Tooltip("Axis around which the planet orbits.")]
        public Vector3 orbitAxis = Vector3.up;

        [Header("3. Custom Saturn Ring Attachment (Optional)")]
        [Tooltip("Drag & drop your existing Saturn Ring 3D GameObject / Transform here. It will automatically attach and move/rotate with Saturn.")]
        public Transform saturnRingObject;

        [Header("4. Scientific Reference Data (Read-Only Info)")]
        [Tooltip("Real astronomical day length in Earth days.")]
        public float realDayLengthDays = 1.0f;

        [Tooltip("Real astronomical orbital period in Earth years.")]
        public float realOrbitPeriodYears = 1.0f;

        [Header("5. Editor Preview Settings")]
        [Tooltip("Allows previewing rotation inside the Unity Scene View without hitting Play.")]
        public bool previewInEditMode = true;

        [Header("6. Operational State")]
        [Tooltip("When paused (e.g., while held by the player in VR), rotation and orbiting are temporarily suspended to prevent tracking jitter.")]
        public bool isPaused = false;

        public void SetPaused(bool paused)
        {
            isPaused = paused;
        }

        private PlanetPreset _lastPreset = PlanetPreset.Custom;

        private void Start()
        {
            ApplyAxialTilt();
            AttachSaturnRingIfAssigned();
        }

        private void OnValidate()
        {
            if (preset != PlanetPreset.Custom && preset != _lastPreset)
            {
                ApplyPreset(preset);
                _lastPreset = preset;
            }
            else
            {
                ApplyAxialTilt();
                AttachSaturnRingIfAssigned();
            }
        }

        /// <summary>
        /// Ensures your custom ring GameObject is parented to Saturn so it rotates and orbits together seamlessly.
        /// </summary>
        public void AttachSaturnRingIfAssigned()
        {
            if (saturnRingObject != null && saturnRingObject.parent != transform)
            {
                saturnRingObject.SetParent(transform, true);
            }
        }

        /// <summary>
        /// Populates scientific astronomical tilt, rotation direction, and scaled visual speeds based on selected planet.
        /// </summary>
        public void ApplyPreset(PlanetPreset planet)
        {
            preset = planet;
            _lastPreset = planet;

            switch (planet)
            {
                case PlanetPreset.Sun:
                    realDayLengthDays = 27.0f;
                    realOrbitPeriodYears = 0f;
                    axialTilt = 7.25f;
                    spinSpeed = 6.0f;
                    orbitSpeed = 0.0f;
                    enableOrbit = false;
                    break;

                case PlanetPreset.Mercury:
                    realDayLengthDays = 58.6f;
                    realOrbitPeriodYears = 0.24f;
                    axialTilt = 0.03f;
                    spinSpeed = 5.0f;
                    orbitSpeed = 22.0f;
                    break;

                case PlanetPreset.Venus:
                    realDayLengthDays = 243.0f;
                    realOrbitPeriodYears = 0.62f;
                    axialTilt = 177.3f;    // Inverted axis (North pole upside-down)
                    spinSpeed = 2.0f;      // Positive spin around inverted axis yields Retrograde rotation
                    orbitSpeed = 16.0f;
                    break;

                case PlanetPreset.Earth:
                    realDayLengthDays = 1.0f;
                    realOrbitPeriodYears = 1.0f;
                    axialTilt = 23.44f;
                    spinSpeed = 30.0f;
                    orbitSpeed = 10.0f;
                    break;

                case PlanetPreset.Mars:
                    realDayLengthDays = 1.03f;
                    realOrbitPeriodYears = 1.88f;
                    axialTilt = 25.19f;
                    spinSpeed = 29.1f;
                    orbitSpeed = 7.2f;
                    break;

                case PlanetPreset.Jupiter:
                    realDayLengthDays = 0.41f; // 9.9 hours
                    realOrbitPeriodYears = 11.86f;
                    axialTilt = 3.13f;
                    spinSpeed = 73.2f;
                    orbitSpeed = 4.2f;
                    break;

                case PlanetPreset.Saturn:
                    realDayLengthDays = 0.45f; // 10.7 hours
                    realOrbitPeriodYears = 29.45f;
                    axialTilt = 26.73f;    // Saturn's 26.73° axial tilt
                    spinSpeed = 66.7f;
                    orbitSpeed = 2.8f;
                    break;

                case PlanetPreset.Uranus:
                    realDayLengthDays = 0.72f;
                    realOrbitPeriodYears = 84.01f;
                    axialTilt = 97.77f;    // Sideways tilt
                    spinSpeed = 41.7f;
                    orbitSpeed = 1.8f;
                    break;

                case PlanetPreset.Neptune:
                    realDayLengthDays = 0.67f;
                    realOrbitPeriodYears = 164.8f;
                    axialTilt = 28.32f;
                    spinSpeed = 44.8f;
                    orbitSpeed = 1.2f;
                    break;

                case PlanetPreset.Moon:
                    realDayLengthDays = 27.3f;
                    realOrbitPeriodYears = 0.075f;
                    axialTilt = 6.68f;
                    spinSpeed = 15.0f;
                    orbitSpeed = 60.0f;
                    break;

                case PlanetPreset.Custom:
                default:
                    break;
            }

            ApplyAxialTilt();
            AttachSaturnRingIfAssigned();
        }

        private void ApplyAxialTilt()
        {
            if (Mathf.Abs(axialTilt) > 0.001f)
            {
                Vector3 currentAngles = transform.localEulerAngles;
                transform.localRotation = Quaternion.Euler(currentAngles.x, currentAngles.y, axialTilt);
            }
        }

        private void Update()
        {
            if (isPaused) return;

            float deltaTime = 0f;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!previewInEditMode) return;

                double currentStep = UnityEditor.EditorApplication.timeSinceStartup;
                if (_lastTime < 0.0001) _lastTime = currentStep;
                deltaTime = (float)(currentStep - _lastTime);
                _lastTime = currentStep;

                if (deltaTime > 0.1f) deltaTime = 0.016f;
            }
            else
            {
                deltaTime = Time.deltaTime;
            }
#else
            deltaTime = Time.deltaTime;
#endif

            if (deltaTime <= 0f) return;

            // 1. Self-Rotation (Axial Spin)
            if (Mathf.Abs(spinSpeed) > 0.0001f)
            {
                transform.Rotate(spinAxis, spinSpeed * deltaTime, Space.Self);
            }

            // 2. Orbital Revolution around Target
            if (enableOrbit && Mathf.Abs(orbitSpeed) > 0.0001f)
            {
                Vector3 pivotPoint = orbitTarget != null ? orbitTarget.position : orbitPivot;
                transform.RotateAround(pivotPoint, orbitAxis, orbitSpeed * deltaTime);
            }
        }

#if UNITY_EDITOR
        private double _lastTime = 0;

        private void OnEnable()
        {
            _lastTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update += EditorUpdate;
        }

        private void OnDisable()
        {
            UnityEditor.EditorApplication.update -= EditorUpdate;
        }

        private void EditorUpdate()
        {
            if (!Application.isPlaying && previewInEditMode)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        [ContextMenu("Apply Preset/Sun")] private void PresetSun() => ApplyPreset(PlanetPreset.Sun);
        [ContextMenu("Apply Preset/Mercury")] private void PresetMercury() => ApplyPreset(PlanetPreset.Mercury);
        [ContextMenu("Apply Preset/Venus")] private void PresetVenus() => ApplyPreset(PlanetPreset.Venus);
        [ContextMenu("Apply Preset/Earth")] private void PresetEarth() => ApplyPreset(PlanetPreset.Earth);
        [ContextMenu("Apply Preset/Mars")] private void PresetMars() => ApplyPreset(PlanetPreset.Mars);
        [ContextMenu("Apply Preset/Jupiter")] private void PresetJupiter() => ApplyPreset(PlanetPreset.Jupiter);
        [ContextMenu("Apply Preset/Saturn")] private void PresetSaturn() => ApplyPreset(PlanetPreset.Saturn);
        [ContextMenu("Apply Preset/Uranus")] private void PresetUranus() => ApplyPreset(PlanetPreset.Uranus);
        [ContextMenu("Apply Preset/Neptune")] private void PresetNeptune() => ApplyPreset(PlanetPreset.Neptune);
        [ContextMenu("Apply Preset/Moon")] private void PresetMoon() => ApplyPreset(PlanetPreset.Moon);
#endif
    }
}
