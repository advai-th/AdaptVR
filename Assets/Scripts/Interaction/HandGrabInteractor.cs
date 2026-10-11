using System.Collections.Generic;
using UnityEngine;
using AdaptiveVR.DataCollection.Core;

namespace AdaptiveVR.PlanetPuzzle
{
    /// <summary>
    /// Attach this component to your VR Hand or Controller GameObjects (Left Hand, Right Hand, or Palm/Pinch point).
    /// Uses trigger collision or proximity checks to detect and grab GrabbablePlanet objects.
    /// Supports both direct script calls (from Meta XR / Unity XR Hand Tracking) and Unity input button polling.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class HandGrabInteractor : MonoBehaviour
    {
        [Header("Grab Configuration")]
        [Tooltip("Optional transform representing the precise pinch or palm holding point. Defaults to this transform.")]
        public Transform grabAnchor;

        [Tooltip("Sphere trigger radius used to detect planets within grabbing range.")]
        public float grabRadius = 0.15f;

        [Header("Input Detection (Optional Standalone Polling)")]
        [Tooltip("If enabled, automatically polls input buttons or keys for grabbing.")]
        public bool autoPollInput = true;

        [Tooltip("Unity legacy input button name (e.g., 'Fire1', 'Submit', or XR Grip).")]
        public string inputButtonName = "Fire1";

        [Tooltip("Keyboard key fallback for testing in Unity Editor (e.g., Space bar or Left Click).")]
        public KeyCode keyboardGrabKey = KeyCode.Space;

        [Header("Current State (Read-Only)")]
        [Tooltip("The planet currently held by this hand.")]
        public GrabbablePlanet heldPlanet;

        [Tooltip("Planets currently within grab proximity.")]
        public List<GrabbablePlanet> nearbyPlanets = new List<GrabbablePlanet>();

        private SphereCollider _triggerCollider;

        private void Awake()
        {
            if (grabAnchor == null)
            {
                grabAnchor = transform;
            }

            SetupTriggerCollider();
        }

        private void SetupTriggerCollider()
        {
            _triggerCollider = GetComponent<SphereCollider>();
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
                _triggerCollider.radius = grabRadius;
            }
        }

        private void Update()
        {
            if (SessionManager.HasInstance && SessionManager.Instance.isSessionPaused)
            {
                if (heldPlanet != null)
                {
                    ReleasePlanet();
                }
                return;
            }

            if (autoPollInput)
            {
                bool isInputPressed = false;

                // Check button
                if (!string.IsNullOrEmpty(inputButtonName))
                {
                    try
                    {
                        if (Input.GetButton(inputButtonName)) isInputPressed = true;
                    }
                    catch
                    {
                        // Ignore undefined axis/button in ProjectSettings
                    }
                }

                // Check keyboard key fallback
                if (Input.GetKey(keyboardGrabKey))
                {
                    isInputPressed = true;
                }

                // Check mouse button 0 if no VR controller
                if (Input.GetMouseButton(0))
                {
                    // If user is clicking near this hand in editor
                }

                if (isInputPressed && heldPlanet == null)
                {
                    GrabNearestPlanet();
                }
                else if (!isInputPressed && heldPlanet != null)
                {
                    ReleasePlanet();
                }
            }
        }

        /// <summary>
        /// Public method to initiate grab. Can be hooked directly to VR Hand Pinch or Controller Grip events.
        /// </summary>
        public void GrabNearestPlanet()
        {
            if (heldPlanet != null) return;

            CleanNearbyList();
            GrabbablePlanet target = GetClosestNearbyPlanet();

            if (target != null)
            {
                GrabPlanet(target);
            }
        }

        /// <summary>
        /// Grabs a specific GrabbablePlanet.
        /// </summary>
        public void GrabPlanet(GrabbablePlanet planet)
        {
            if (planet == null || heldPlanet != null) return;

            heldPlanet = planet;
            heldPlanet.OnGrabStart(grabAnchor != null ? grabAnchor : transform);
        }

        /// <summary>
        /// Releases the currently held planet. Can be hooked directly to VR Hand Release / Unpinch events.
        /// </summary>
        public void ReleasePlanet()
        {
            if (heldPlanet == null) return;

            GrabbablePlanet planetToRelease = heldPlanet;
            heldPlanet = null;
            planetToRelease.OnGrabEnd();
        }

        /// <summary>
        /// Toggles between Grab and Release. Useful for single-click toggle actions.
        /// </summary>
        public void ToggleGrab()
        {
            if (heldPlanet != null)
            {
                ReleasePlanet();
            }
            else
            {
                GrabNearestPlanet();
            }
        }

        private GrabbablePlanet GetClosestNearbyPlanet()
        {
            GrabbablePlanet best = null;
            float minDistance = float.MaxValue;
            Vector3 anchorPos = grabAnchor != null ? grabAnchor.position : transform.position;

            foreach (var planet in nearbyPlanets)
            {
                if (planet == null) continue;
                float dist = Vector3.Distance(anchorPos, planet.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    best = planet;
                }
            }

            return best;
        }

        private void CleanNearbyList()
        {
            nearbyPlanets.RemoveAll(p => p == null || !p.gameObject.activeInHierarchy);
        }

        private void OnTriggerEnter(Collider other)
        {
            GrabbablePlanet planet = other.GetComponentInParent<GrabbablePlanet>();
            if (planet != null && !nearbyPlanets.Contains(planet))
            {
                nearbyPlanets.Add(planet);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            GrabbablePlanet planet = other.GetComponentInParent<GrabbablePlanet>();
            if (planet != null && nearbyPlanets.Contains(planet))
            {
                nearbyPlanets.Remove(planet);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = heldPlanet != null ? Color.green : Color.yellow;
            Vector3 pos = grabAnchor != null ? grabAnchor.position : transform.position;
            Gizmos.DrawWireSphere(pos, grabRadius);
        }
    }
}
