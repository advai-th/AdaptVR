using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveVR.Companion
{
    public enum BotExpression
    {
        Idle,
        Talking,
        Happy,
        Thinking
    }

    public enum ChirpType
    {
        Greeting,
        Happy,
        Talking,
        Thinking
    }

    /// <summary>
    /// Production-grade Unity VR script for a cute, floating futuristic companion bot.
    /// - Gently follows the player over the shoulder with smooth floating/bobbing physics.
    /// - Features animated glowing blue eyes and mouth directly on the face (NO visor or background box).
    /// - Animated expression states: Idle (realistic blinking), Talking (mouth flaps), Happy (celebration ^ ^), Thinking.
    /// - World-space hint/speech bubble with typewriter text animation.
    /// - Built-in procedural robotic chirp audio synthesizer (R2-D2 / Wall-E style).
    /// - Optional movable hands with procedural gesturing and waving.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]
    public class CompanionBot : MonoBehaviour
    {
        #region Inspector Fields

        [Header("--- Target & Following ---")]
        [Tooltip("The player's head or VR Main Camera transform to follow. If unassigned, automatically finds Camera.main.")]
        public Transform followTarget;

        [Tooltip("Position offset relative to the player's head (X=Right, Y=Up, Z=Forward).")]
        public Vector3 shoulderOffset = new Vector3(0.50f, 0.10f, 0.45f);

        [Tooltip("True for right shoulder, False for left shoulder.")]
        public bool followRightShoulder = true;

        [Tooltip("Movement follow smoothness. Higher = gentler lag, Lower = snappier.")]
        [Range(0.1f, 1.5f)]
        public float followSmoothTime = 0.45f;

        [Tooltip("Rotation smoothness when looking toward the player or travel direction.")]
        [Range(1f, 15f)]
        public float rotationSpeed = 4.5f;

        [Tooltip("Vertical floating bob frequency (cycles per second).")]
        [Range(0.2f, 3f)]
        public float hoverBobFrequency = 1.1f;

        [Tooltip("Vertical floating bob amplitude in meters.")]
        [Range(0.01f, 0.15f)]
        public float hoverBobAmplitude = 0.035f;

        [Tooltip("Subtle banking tilt angle (degrees) when the bot turns or moves.")]
        [Range(0f, 35f)]
        public float bankTiltAngle = 14f;

        [Tooltip("Minimum safe distance from the headset. If the player steps too close, bot smoothly backs away.")]
        [Range(0.2f, 0.8f)]
        public float minComfortDistance = 0.42f;

        [Tooltip("If true, the bot gently turns to look at the player.")]
        public bool lookAtPlayer = true;


        [Header("--- Facial Features (Eyes & Mouth) ---")]
        [Tooltip("Head or face transform anchor on your 3D model. If null, uses this bot root.")]
        public Transform headAnchor;

        [Tooltip("Renderer for the Left Eye quad. Auto-generated if unassigned.")]
        public Renderer leftEyeRenderer;

        [Tooltip("Renderer for the Right Eye quad. Auto-generated if unassigned.")]
        public Renderer rightEyeRenderer;

        [Tooltip("Renderer for the Mouth quad. Auto-generated if unassigned.")]
        public Renderer mouthRenderer;

        [Tooltip("Electric/Neon blue color for the eyes and mouth.")]
        public Color blueFeatureColor = new Color(0.0f, 0.88f, 1.0f, 1.0f);

        [Tooltip("HDR emission glow intensity for VR bloom.")]
        [Range(1f, 5f)]
        public float emissionIntensity = 2.2f;

        [Tooltip("Local position offset for eyes relative to head anchor.")]
        public Vector3 eyeOffset = new Vector3(0f, 0.10f, 0.22f);

        [Tooltip("Horizontal spacing between left and right eyes.")]
        [Range(0.04f, 0.35f)]
        public float eyeSpacing = 0.12f;

        [Tooltip("Width and height of each eye quad.")]
        public Vector2 eyeSize = new Vector2(0.07f, 0.07f);

        [Tooltip("Local position offset for mouth relative to head anchor.")]
        public Vector3 mouthOffset = new Vector3(0f, 0.01f, 0.22f);

        [Tooltip("Width and height of the mouth quad.")]
        public Vector2 mouthSize = new Vector2(0.11f, 0.055f);


        [Header("--- Optional Custom Textures (Leave empty for procedural) ---")]
        public Texture2D customEyeOpen;
        public Texture2D customEyeClosed;
        public Texture2D customEyeHappy;
        public Texture2D customMouthSmile;
        public Texture2D customMouthTalking;
        public Texture2D customMouthHappy;


        [Header("--- Expression & Animation ---")]
        [Tooltip("Current facial expression.")]
        public BotExpression currentExpression = BotExpression.Idle;

        [Tooltip("Minimum time (seconds) between idle blinks.")]
        [Range(1.5f, 6f)]
        public float minBlinkInterval = 2.8f;

        [Tooltip("Maximum time (seconds) between idle blinks.")]
        [Range(3f, 10f)]
        public float maxBlinkInterval = 6.0f;

        [Tooltip("Mouth flapping speed when talking (flaps per second).")]
        [Range(4f, 14f)]
        public float talkMouthSpeed = 8.5f;


        [Header("--- Optional Movable Hands (Socket Rotation Only) ---")]
        [Tooltip("Optional Left Hand/Socket transform on your bot model. Leave empty if model has no hands.")]
        public Transform leftHand;

        [Tooltip("Optional Right Hand/Socket transform on your bot model. Leave empty if model has no hands.")]
        public Transform rightHand;

        [Tooltip("Master speed multiplier for hand gesture animations.")]
        [Range(0.5f, 5.0f)]
        public float handAnimationSpeed = 2.4f;

        [Tooltip("Master movement amount/scale for all hand gestures.")]
        [Range(0.2f, 2.5f)]
        public float handMotionScale = 1.2f;

        [Tooltip("Invert Left Hand sidewards (Y) rotation if needed for your socket.")]
        public bool invertLeftY = false;

        [Tooltip("Invert Right Hand sidewards (Y) rotation if needed for your socket.")]
        public bool invertRightY = false;


        [Header("--- Speech / Hint Bubble ---")]
        [Tooltip("Enable world-space hint/speech bubble above the bot.")]
        public bool enableSpeechBubble = true;

        [Tooltip("Preview the speech bubble in Scene view / Edit mode so you can adjust its position.")]
        public bool previewBubbleInEditor = true;

        [Tooltip("Offset for the speech bubble relative to the bot root. You can also drag the BotSpeechBubble child in Scene View!")]
        public Vector3 speechBubbleOffset = new Vector3(0f, 0.38f, 0f);

        [Tooltip("Speed of the typewriter text reveal (characters per second).")]
        [Range(15f, 80f)]
        public float typewriterSpeed = 35f;


        [Header("--- Audio & SFX ---")]
        [Tooltip("AudioSource component for voice lines and chirps. Auto-created if null.")]
        public AudioSource audioSource;

        [Tooltip("Generate cute procedural robotic beeps & chirps automatically if no audio clip is provided.")]
        public bool generateProceduralChirps = true;

        [Tooltip("Volume of procedural chirps.")]
        [Range(0f, 1f)]
        public float chirpVolume = 0.55f;

        [Tooltip("Optional voice/SFX clip for greetings.")]
        public AudioClip greetingAudioClip;

        [Tooltip("Optional voice/SFX clip for happy celebrations.")]
        public AudioClip celebrationAudioClip;


        [Header("--- Soft Floating Glow (Antigravity Halo) ---")]
        [Tooltip("Enable a gentle, soft glowing antigravity halo disc beneath the bot to show it's floating.")]
        public bool enableFloatingGlow = true;

        [Tooltip("Position offset beneath the bot base.")]
        public Vector3 glowOffset = new Vector3(0f, -0.16f, 0f);

        [Tooltip("Diameter of the soft circular halo disc.")]
        [Range(0.1f, 0.8f)]
        public float haloDiameter = 0.28f;

        [Tooltip("Color tint for the soft levitation aura.")]
        public Color glowColor = new Color(0.0f, 0.88f, 1.0f, 0.85f);

        [Tooltip("Brightness / intensity of the soft aura.")]
        [Range(0.5f, 3.5f)]
        public float glowIntensity = 1.6f;

        [Tooltip("Soft point light beneath the bot illuminating the ground/table in VR.")]
        public bool enableSoftLight = true;

        [Tooltip("Intensity of the soft point light.")]
        [Range(0.1f, 2.5f)]
        public float softLightIntensity = 1.0f;

        #endregion

        #region Internal State

        // Procedural Textures
        private Texture2D _texEyeOpen;
        private Texture2D _texEyeHalf;
        private Texture2D _texEyeClosed;
        private Texture2D _texEyeHappy;
        private Texture2D _texEyeThinking;

        private Texture2D _texMouthSmile;
        private Texture2D _texMouthTalkSmall;
        private Texture2D _texMouthTalkOpen;
        private Texture2D _texMouthHappyWide;
        private Texture2D _texMouthThinking;

        private Material _faceMaterial;

        // Soft Floating Glow components
        private GameObject _floatingGlowRoot;
        private Renderer _haloRenderer;
        private Light _softGlowLight;
        private Material _haloMaterial;

        // Smooth follow velocity references
        private Vector3 _currentVelocity;
        private float _hoverTimer;
        private float _nextBlinkTime;
        private bool _isBlinking;
        private Coroutine _blinkCoroutine;
        private Coroutine _expressionResetCoroutine;
        private Coroutine _typewriterCoroutine;

        // Joyful celebration wiggle
        private float _celebrationTimer;
        private bool _isCelebrating;

        // Hand rest rotations & positions
        private Vector3 _leftHandInitPos;
        private Quaternion _leftHandInitRot;
        private Vector3 _rightHandInitPos;
        private Quaternion _rightHandInitRot;
        private bool _hasHands;

        // Speech Bubble UI components
        private GameObject _speechBubbleObj;
        private CanvasGroup _speechBubbleCanvasGroup;
        private Text _speechBubbleText;
        private Coroutine _bubbleDismissCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-locate Player Headset / Main Camera if unassigned
            if (followTarget == null && Camera.main != null)
            {
                followTarget = Camera.main.transform;
            }

            if (headAnchor == null)
            {
                headAnchor = transform;
            }

            // Ensure AudioSource exists
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 1.0f; // Full 3D spatialized VR audio
                    audioSource.minDistance = 0.5f;
                    audioSource.maxDistance = 10f;
                }
            }

            // Record initial hand transforms if assigned
            _hasHands = (leftHand != null || rightHand != null);
            if (leftHand != null)
            {
                _leftHandInitPos = leftHand.localPosition;
                _leftHandInitRot = leftHand.localRotation;
            }
            if (rightHand != null)
            {
                _rightHandInitPos = rightHand.localPosition;
                _rightHandInitRot = rightHand.localRotation;
            }

            // Initialize procedural textures and setup face quads
            InitializeFaceTextures();
            EnsureFaceRenderers();

            // Setup speech bubble
            if (enableSpeechBubble)
            {
                EnsureSpeechBubble();
            }

            // Setup soft floating glow
            if (enableFloatingGlow)
            {
                EnsureFloatingGlow();
            }

            ScheduleNextBlink();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                // Play a soft greeting chirp on startup
                if (generateProceduralChirps || greetingAudioClip != null)
                {
                    PlayChirp(ChirpType.Greeting);
                }
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                HandleFloatingAndFollowing();
                HandleFaceAnimation();
                HandleOptionalHands();
                HandleSpeechBubbleBillboard();
                HandleFloatingGlowAnimation();
            }
            else
            {
                HandleEditModePreview();
            }
        }

        private void OnValidate()
        {
            if (!enableFloatingGlow)
            {
                RemoveFloatingGlow();
            }
            if (!enableSpeechBubble)
            {
                RemoveSpeechBubble();
            }
        }

        private void HandleEditModePreview()
        {
            // Only update elements IF they exist in hierarchy - NEVER resurrect deleted objects every frame!
            Transform bubbleTrans = transform.Find("BotSpeechBubble");
            if (bubbleTrans != null)
            {
                _speechBubbleObj = bubbleTrans.gameObject;
                if (_speechBubbleCanvasGroup == null) _speechBubbleCanvasGroup = _speechBubbleObj.GetComponent<CanvasGroup>();

                // Detect if user moved the speech bubble in Scene view with the Move tool
                if (_speechBubbleObj.transform.hasChanged)
                {
                    speechBubbleOffset = _speechBubbleObj.transform.localPosition;
                    _speechBubbleObj.transform.hasChanged = false;
                }

                if (_speechBubbleCanvasGroup != null)
                {
                    _speechBubbleCanvasGroup.alpha = previewBubbleInEditor ? 1f : 0f;
                }

                HandleSpeechBubbleBillboard();
            }
            else
            {
                _speechBubbleObj = null;
            }

            Transform glowTrans = transform.Find("FloatingGlow");
            if (glowTrans != null)
            {
                _floatingGlowRoot = glowTrans.gameObject;
                // If user moved glow in scene view, sync offset
                if (_floatingGlowRoot.transform.hasChanged)
                {
                    glowOffset = _floatingGlowRoot.transform.localPosition;
                    _floatingGlowRoot.transform.hasChanged = false;
                }
            }
            else
            {
                _floatingGlowRoot = null;
            }
        }

        /// <summary>
        /// Deletes the floating halo completely from the bot hierarchy.
        /// </summary>
        public void RemoveFloatingGlow()
        {
            Transform existing = transform.Find("FloatingGlow");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            Transform oldBooster = transform.Find("BoosterRoot");
            if (oldBooster != null)
            {
                if (Application.isPlaying) Destroy(oldBooster.gameObject);
                else DestroyImmediate(oldBooster.gameObject);
            }
            _floatingGlowRoot = null;
            _haloRenderer = null;
            _softGlowLight = null;
        }

        /// <summary>
        /// Deletes the speech bubble completely from the bot hierarchy.
        /// </summary>
        public void RemoveSpeechBubble()
        {
            Transform existing = transform.Find("BotSpeechBubble");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            _speechBubbleObj = null;
            _speechBubbleCanvasGroup = null;
            _speechBubbleText = null;
        }

        #endregion

        #region Floating & Movement Physics

        private void HandleFloatingAndFollowing()
        {
            if (followTarget == null)
            {
                // Fallback search for camera
                if (Camera.main != null) followTarget = Camera.main.transform;
                else return;
            }

            _hoverTimer += Time.deltaTime * hoverBobFrequency;

            // Compute ideal shoulder target position in world space
            Vector3 sideSignOffset = shoulderOffset;
            if (!followRightShoulder) sideSignOffset.x = -sideSignOffset.x;

            Vector3 targetHeadPos = followTarget.position;
            Quaternion targetHeadRot = Quaternion.Euler(0f, followTarget.eulerAngles.y, 0f); // Yaw only

            Vector3 desiredWorldPos = targetHeadPos + targetHeadRot * sideSignOffset;

            // Organic vertical bobbing using multi-sine waves
            float bobY = Mathf.Sin(_hoverTimer * Mathf.PI * 2f) * hoverBobAmplitude
                       + Mathf.Sin(_hoverTimer * Mathf.PI * 4.3f) * (hoverBobAmplitude * 0.25f);
            desiredWorldPos.y += bobY;

            // Anti-clipping personal space check: ensure bot never gets closer than minComfortDistance
            Vector3 toBot = desiredWorldPos - targetHeadPos;
            float distToHead = toBot.magnitude;
            if (distToHead < minComfortDistance && distToHead > 0.001f)
            {
                desiredWorldPos = targetHeadPos + (toBot / distToHead) * minComfortDistance;
            }

            // Add joyful celebration bounce if celebrating
            if (_isCelebrating)
            {
                _celebrationTimer += Time.deltaTime * 6f;
                desiredWorldPos.y += Mathf.Abs(Mathf.Sin(_celebrationTimer * Mathf.PI)) * 0.06f;
            }

            // Smooth position dampening
            Vector3 previousPos = transform.position;
            transform.position = Vector3.SmoothDamp(transform.position, desiredWorldPos, ref _currentVelocity, followSmoothTime);

            // Compute velocity direction for banking
            Vector3 horizontalVel = (transform.position - previousPos) / Mathf.Max(Time.deltaTime, 0.001f);
            horizontalVel.y = 0f;

            // Rotation handling
            Quaternion targetRotation;
            if (lookAtPlayer)
            {
                Vector3 lookDir = (targetHeadPos - transform.position);
                lookDir.y = 0f; // Keep level
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    targetRotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
                else
                {
                    targetRotation = transform.rotation;
                }
            }
            else
            {
                targetRotation = targetHeadRot;
            }

            // Subtle banking roll when moving laterally
            if (horizontalVel.magnitude > 0.05f)
            {
                Vector3 localVel = transform.InverseTransformDirection(horizontalVel);
                float bankRoll = -localVel.x * bankTiltAngle;
                targetRotation *= Quaternion.Euler(0f, 0f, Mathf.Clamp(bankRoll, -bankTiltAngle, bankTiltAngle));
            }

            // Add celebration wiggle roll
            if (_isCelebrating)
            {
                float wiggleRoll = Mathf.Sin(_celebrationTimer * Mathf.PI * 2f) * 12f;
                targetRotation *= Quaternion.Euler(0f, 0f, wiggleRoll);
            }

            // Smooth rotation slerp
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        #endregion

        #region Face & Expression Animation

        private void InitializeFaceTextures()
        {
            // Procedural blue textures with transparent background (no visor quad)
            _texEyeOpen = customEyeOpen != null ? customEyeOpen : BotFaceTextureUtility.CreateEyeOpenTexture(blueFeatureColor);
            _texEyeHalf = BotFaceTextureUtility.CreateEyeHalfBlinkTexture(blueFeatureColor);
            _texEyeClosed = customEyeClosed != null ? customEyeClosed : BotFaceTextureUtility.CreateEyeClosedTexture(blueFeatureColor);
            _texEyeHappy = customEyeHappy != null ? customEyeHappy : BotFaceTextureUtility.CreateEyeHappyTexture(blueFeatureColor);
            _texEyeThinking = BotFaceTextureUtility.CreateEyeThinkingTexture(blueFeatureColor);

            _texMouthSmile = customMouthSmile != null ? customMouthSmile : BotFaceTextureUtility.CreateMouthSmileTexture(blueFeatureColor);
            _texMouthTalkSmall = customMouthTalking != null ? customMouthTalking : BotFaceTextureUtility.CreateMouthTalkingSmallTexture(blueFeatureColor);
            _texMouthTalkOpen = BotFaceTextureUtility.CreateMouthTalkingOpenTexture(blueFeatureColor);
            _texMouthHappyWide = customMouthHappy != null ? customMouthHappy : BotFaceTextureUtility.CreateMouthHappyWideTexture(blueFeatureColor);
            _texMouthThinking = BotFaceTextureUtility.CreateMouthThinkingTexture(blueFeatureColor);

            // Create shared emissive unlit material
            _faceMaterial = BotFaceTextureUtility.CreateEmissiveFaceMaterial(blueFeatureColor, emissionIntensity);
        }

        /// <summary>
        /// Ensures left eye, right eye, and mouth renderers exist and are configured.
        /// </summary>
        public void EnsureFaceRenderers()
        {
            if (headAnchor == null) headAnchor = transform;

            if (leftEyeRenderer == null)
            {
                leftEyeRenderer = CreateFeatureQuad("LeftEye", eyeOffset + new Vector3(-eyeSpacing * 0.5f, 0f, 0f), eyeSize);
            }
            if (rightEyeRenderer == null)
            {
                rightEyeRenderer = CreateFeatureQuad("RightEye", eyeOffset + new Vector3(eyeSpacing * 0.5f, 0f, 0f), eyeSize);
            }
            if (mouthRenderer == null)
            {
                mouthRenderer = CreateFeatureQuad("Mouth", mouthOffset, mouthSize);
            }

            // Assign material instances
            AssignTextureToRenderer(leftEyeRenderer, _texEyeOpen);
            AssignTextureToRenderer(rightEyeRenderer, _texEyeOpen);
            AssignTextureToRenderer(mouthRenderer, _texMouthSmile);
        }

        private Renderer CreateFeatureQuad(string featureName, Vector3 localPos, Vector2 size)
        {
            // Check if existing child with same name exists
            Transform existing = headAnchor.Find(featureName);
            if (existing != null)
            {
                Renderer r = existing.GetComponent<Renderer>();
                if (r != null) return r;
            }

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = featureName;
            quad.transform.SetParent(headAnchor, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            // Remove unwanted collider
            Collider col = quad.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            Renderer ren = quad.GetComponent<Renderer>();
            if (_faceMaterial == null)
            {
                _faceMaterial = BotFaceTextureUtility.CreateEmissiveFaceMaterial(blueFeatureColor, emissionIntensity);
            }
            ren.material = new Material(_faceMaterial);
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ren.receiveShadows = false;

            return ren;
        }

        private void HandleFaceAnimation()
        {
            switch (currentExpression)
            {
                case BotExpression.Idle:
                    UpdateIdleBlinking();
                    SetMouthTexture(_texMouthSmile);
                    break;

                case BotExpression.Talking:
                    UpdateTalkingAnimation();
                    break;

                case BotExpression.Happy:
                    SetEyeTextures(_texEyeHappy, _texEyeHappy);
                    SetMouthTexture(_texMouthHappyWide);
                    break;

                case BotExpression.Thinking:
                    SetEyeTextures(_texEyeThinking, _texEyeOpen);
                    SetMouthTexture(_texMouthThinking);
                    break;
            }
        }

        private void UpdateIdleBlinking()
        {
            if (Time.time >= _nextBlinkTime && !_isBlinking)
            {
                if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = StartCoroutine(PerformBlinkSequence());
            }
        }

        private IEnumerator PerformBlinkSequence()
        {
            _isBlinking = true;

            // Half-closed
            SetEyeTextures(_texEyeHalf, _texEyeHalf);
            yield return new WaitForSeconds(0.045f);

            // Fully closed
            SetEyeTextures(_texEyeClosed, _texEyeClosed);
            yield return new WaitForSeconds(0.065f);

            // Half-open
            SetEyeTextures(_texEyeHalf, _texEyeHalf);
            yield return new WaitForSeconds(0.045f);

            // Restored open
            SetEyeTextures(_texEyeOpen, _texEyeOpen);

            ScheduleNextBlink();
            _isBlinking = false;
        }

        private void ScheduleNextBlink()
        {
            _nextBlinkTime = Time.time + Random.Range(minBlinkInterval, maxBlinkInterval);
        }

        private void UpdateTalkingAnimation()
        {
            // Mouth flap cycle between slightly open, wide, and smile
            float flap = Mathf.PingPong(Time.time * talkMouthSpeed, 1.0f);
            if (flap < 0.35f)
            {
                SetMouthTexture(_texMouthTalkSmall);
            }
            else if (flap < 0.75f)
            {
                SetMouthTexture(_texMouthTalkOpen);
            }
            else
            {
                SetMouthTexture(_texMouthSmile);
            }

            // Eyes blink normally while talking
            if (!_isBlinking)
            {
                SetEyeTextures(_texEyeOpen, _texEyeOpen);
                if (Time.time >= _nextBlinkTime)
                {
                    StartCoroutine(PerformBlinkSequence());
                }
            }
        }

        private void SetEyeTextures(Texture2D leftTex, Texture2D rightTex)
        {
            AssignTextureToRenderer(leftEyeRenderer, leftTex);
            AssignTextureToRenderer(rightEyeRenderer, rightTex);
        }

        private void SetMouthTexture(Texture2D mouthTex)
        {
            AssignTextureToRenderer(mouthRenderer, mouthTex);
        }

        private void AssignTextureToRenderer(Renderer r, Texture2D tex)
        {
            if (r == null || tex == null) return;
            Material mat = r.material;
            if (mat != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
            }
        }

        #endregion

        #region Optional Hands Gestures

        private void HandleOptionalHands()
        {
            if (!_hasHands) return;

            float t = Time.time * handAnimationSpeed;
            float scale = handMotionScale;

            // --- LEFT HAND ---
            if (leftHand != null)
            {
                // Lock position strictly to socket - ZERO positional displacement
                leftHand.localPosition = _leftHandInitPos;

                float rotX = 0f; // Forward (+) / Backward (-)
                float rotY = 0f; // Raises sidewards

                switch (currentExpression)
                {
                    case BotExpression.Idle:
                        // Gentle natural idle breathing sway with subtle harmonic micro-shifts
                        rotX = (Mathf.Sin(t * 1.0f) * 7f + Mathf.Sin(t * 2.3f) * 3f) * scale;
                        rotY = (8f + Mathf.Sin(t * 0.8f) * 4f) * scale;
                        break;

                    case BotExpression.Talking:
                        // Lively communicative gestures: swings forward to explain and emphasize points
                        rotX = (24f + Mathf.Sin(t * 3.4f) * 16f + Mathf.Sin(t * 6.8f) * 6f) * scale;
                        rotY = (16f + Mathf.Cos(t * 1.8f) * 9f) * scale;
                        break;

                    case BotExpression.Happy:
                        // High-energy celebration cheer: raised high to the side with excited waving
                        rotY = (45f + Mathf.Sin(t * 5.2f) * 20f) * scale;
                        rotX = (22f + Mathf.Sin(t * 4.0f) * 14f) * scale;
                        break;

                    case BotExpression.Thinking:
                        // Thoughtful asymmetric stance: resting hand tilted back while pondering
                        rotX = (-10f + Mathf.Cos(t * 1.1f) * 4f) * scale;
                        rotY = (14f + Mathf.Sin(t * 0.9f) * 4f) * scale;
                        break;
                }

                if (invertLeftY) rotY = -rotY;

                // Apply ONLY X and Y rotation (Z is locked at 0)
                Quaternion targetHandRot = _leftHandInitRot * Quaternion.Euler(rotX, rotY, 0f);
                leftHand.localRotation = Quaternion.Slerp(leftHand.localRotation, targetHandRot, Time.deltaTime * 6f);
            }

            // --- RIGHT HAND ---
            if (rightHand != null)
            {
                // Lock position strictly to socket - ZERO positional displacement
                rightHand.localPosition = _rightHandInitPos;

                float rotX = 0f; // Forward (+) / Backward (-)
                float rotY = 0f; // Raises sidewards (mirrored for right)

                switch (currentExpression)
                {
                    case BotExpression.Idle:
                        // Gentle breathing sway out-of-phase with left hand for organic asymmetry
                        rotX = (Mathf.Cos(t * 1.1f) * 7f + Mathf.Sin(t * 2.5f) * 3f) * scale;
                        rotY = (-8f - Mathf.Sin(t * 0.8f) * 4f) * scale;
                        break;

                    case BotExpression.Talking:
                        // Alternating communicative gestures: points forward with rhythmic cadence
                        rotX = (20f + Mathf.Cos(t * 3.0f) * 16f - Mathf.Sin(t * 6.2f) * 6f) * scale;
                        rotY = (-16f - Mathf.Sin(t * 1.8f) * 9f) * scale;
                        break;

                    case BotExpression.Happy:
                        // High-energy celebration cheer: raised outward with energetic mirrored waving
                        rotY = (-45f - Mathf.Sin(t * 5.2f + 0.4f) * 20f) * scale;
                        rotX = (22f + Mathf.Cos(t * 4.0f) * 14f) * scale;
                        break;

                    case BotExpression.Thinking:
                        // Inquisitive "thinking chin-scratch" pose: right hand lifted forward & up toward the face
                        rotX = (38f + Mathf.Sin(t * 1.4f) * 6f) * scale;
                        rotY = (-10f + Mathf.Sin(t * 1.0f) * 4f) * scale;
                        break;
                }

                if (invertRightY) rotY = -rotY;

                // Apply ONLY X and Y rotation (Z is locked at 0)
                Quaternion targetHandRot = _rightHandInitRot * Quaternion.Euler(rotX, rotY, 0f);
                rightHand.localRotation = Quaternion.Slerp(rightHand.localRotation, targetHandRot, Time.deltaTime * 6f);
            }
        }

        #endregion

        #region Speech Bubble System

        /// <summary>
        /// Ensures world-space speech bubble exists and is configured.
        /// Usable in both Edit Mode (for visual repositioning) and Play Mode.
        /// </summary>
        public void EnsureSpeechBubble()
        {
            if (!enableSpeechBubble)
            {
                if (_speechBubbleObj != null)
                {
                    if (Application.isPlaying) Destroy(_speechBubbleObj);
                    else DestroyImmediate(_speechBubbleObj);
                }
                return;
            }

            Transform existing = transform.Find("BotSpeechBubble");
            if (existing != null)
            {
                _speechBubbleObj = existing.gameObject;
                _speechBubbleCanvasGroup = _speechBubbleObj.GetComponent<CanvasGroup>();
                Transform textChild = _speechBubbleObj.transform.Find("HintText");
                if (textChild != null) _speechBubbleText = textChild.GetComponent<Text>();
            }
            else
            {
                _speechBubbleObj = new GameObject("BotSpeechBubble");
                _speechBubbleObj.transform.SetParent(transform, false);
                _speechBubbleObj.transform.localPosition = speechBubbleOffset;

                Canvas canvas = _speechBubbleObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                _speechBubbleCanvasGroup = _speechBubbleObj.AddComponent<CanvasGroup>();

                RectTransform canvasRect = _speechBubbleObj.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(340f, 130f);
                canvasRect.localScale = Vector3.one * 0.0018f; // Scaled down comfortably for VR

                // Rounded background card with anti-aliased glowing cyan border (no hard square corners)
                GameObject bgObj = new GameObject("Background");
                bgObj.transform.SetParent(_speechBubbleObj.transform, false);
                Image bgImage = bgObj.AddComponent<Image>();
                bgImage.sprite = BotFaceTextureUtility.CreateRoundedCardSprite(340, 130, 24f, new Color(0.02f, 0.06f, 0.14f, 0.90f), new Color(0.0f, 0.88f, 1.0f, 0.90f), 3.0f);
                bgImage.color = Color.white;

                RectTransform bgRect = bgObj.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;

                // Text component
                GameObject textObj = new GameObject("HintText");
                textObj.transform.SetParent(_speechBubbleObj.transform, false);
                _speechBubbleText = textObj.AddComponent<Text>();
                _speechBubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_speechBubbleText.font == null) _speechBubbleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _speechBubbleText.fontSize = 20;
                _speechBubbleText.alignment = TextAnchor.MiddleCenter;
                _speechBubbleText.color = new Color(0.9f, 0.98f, 1.0f, 1.0f);
                _speechBubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _speechBubbleText.verticalOverflow = VerticalWrapMode.Truncate;
                _speechBubbleText.text = "Hello! Try placing Mercury in the first orbit.";

                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = new Vector2(-28f, -20f); // Margins
            }

            // Sync position with speechBubbleOffset
            _speechBubbleObj.transform.localPosition = speechBubbleOffset;

            // Visibility in Edit vs Play mode
            if (!Application.isPlaying)
            {
                if (_speechBubbleCanvasGroup != null)
                {
                    _speechBubbleCanvasGroup.alpha = previewBubbleInEditor ? 1f : 0f;
                }
            }
            else
            {
                // In play mode, initially hidden until ShowHint is called
                if (_speechBubbleCanvasGroup != null && string.IsNullOrEmpty(_speechBubbleText?.text))
                {
                    _speechBubbleCanvasGroup.alpha = 0f;
                }
            }
        }

        private void HandleSpeechBubbleBillboard()
        {
            if (_speechBubbleObj == null || _speechBubbleCanvasGroup == null || _speechBubbleCanvasGroup.alpha <= 0.01f)
                return;

            Transform target = followTarget != null ? followTarget : (Camera.main != null ? Camera.main.transform : null);
#if UNITY_EDITOR
            if (target == null && !Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera != null)
            {
                target = UnityEditor.SceneView.lastActiveSceneView.camera.transform;
            }
#endif
            if (target != null)
            {
                // Face directly toward player head without horizontal mirroring (front of canvas faces camera)
                Vector3 toTarget = target.position - _speechBubbleObj.transform.position;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    _speechBubbleObj.transform.rotation = Quaternion.LookRotation(-toTarget, Vector3.up);
                }
            }
        }

        #endregion

        #region Soft Floating Glow (Antigravity Halo)

        /// <summary>
        /// Ensures the soft antigravity halo disc and soft point light exist beneath the bot.
        /// Automatically removes any old harsh rocket booster flame quads.
        /// </summary>
        public void EnsureFloatingGlow()
        {
            // Automatically clean up any old BoosterRoot with harsh rocket quads
            Transform oldBooster = transform.Find("BoosterRoot");
            if (oldBooster != null)
            {
                if (Application.isPlaying) Destroy(oldBooster.gameObject);
                else DestroyImmediate(oldBooster.gameObject);
            }

            if (!enableFloatingGlow)
            {
                if (_floatingGlowRoot != null)
                {
                    if (Application.isPlaying) Destroy(_floatingGlowRoot);
                    else DestroyImmediate(_floatingGlowRoot);
                }
                return;
            }

            Transform existing = transform.Find("FloatingGlow");
            if (existing != null)
            {
                _floatingGlowRoot = existing.gameObject;
            }
            else
            {
                _floatingGlowRoot = new GameObject("FloatingGlow");
                _floatingGlowRoot.transform.SetParent(transform, false);
                _floatingGlowRoot.transform.localPosition = glowOffset;
                _floatingGlowRoot.transform.localRotation = Quaternion.identity;
            }

            // Create Additive glow material (Additive blending = ZERO black box borders!)
            if (_haloMaterial == null)
            {
                _haloMaterial = BotFaceTextureUtility.CreateAdditiveGlowMaterial(glowColor, glowIntensity);
                Texture2D haloTex = BotFaceTextureUtility.CreateSoftGlowHaloTexture(glowColor);
                _haloMaterial.mainTexture = haloTex;
                if (_haloMaterial.HasProperty("_BaseMap")) _haloMaterial.SetTexture("_BaseMap", haloTex);
                if (_haloMaterial.HasProperty("_EmissionMap")) _haloMaterial.SetTexture("_EmissionMap", haloTex);
            }

            // Halo horizontal disc quad
            Transform haloTrans = _floatingGlowRoot.transform.Find("LevitationHalo");
            GameObject haloObj;
            if (haloTrans != null)
            {
                haloObj = haloTrans.gameObject;
            }
            else
            {
                haloObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                haloObj.name = "LevitationHalo";
                haloObj.transform.SetParent(_floatingGlowRoot.transform, false);
                haloObj.transform.localPosition = Vector3.zero;
                haloObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Horizontal plane
                haloObj.transform.localScale = new Vector3(haloDiameter, haloDiameter, 1f);

                Collider col = haloObj.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
            }

            _haloRenderer = haloObj.GetComponent<Renderer>();
            _haloRenderer.material = _haloMaterial;
            _haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _haloRenderer.receiveShadows = false;

            // Soft Point Light
            if (enableSoftLight)
            {
                Transform lightTrans = _floatingGlowRoot.transform.Find("SoftAuraLight");
                GameObject lightObj;
                if (lightTrans == null)
                {
                    lightObj = new GameObject("SoftAuraLight");
                    lightObj.transform.SetParent(_floatingGlowRoot.transform, false);
                    lightObj.transform.localPosition = new Vector3(0f, -0.04f, 0f);
                }
                else
                {
                    lightObj = lightTrans.gameObject;
                }

                _softGlowLight = lightObj.GetComponent<Light>();
                if (_softGlowLight == null) _softGlowLight = lightObj.AddComponent<Light>();
                _softGlowLight.type = LightType.Point;
                _softGlowLight.color = glowColor;
                _softGlowLight.intensity = softLightIntensity;
                _softGlowLight.range = 0.85f;
                _softGlowLight.shadows = LightShadows.None;
            }
        }

        private void HandleFloatingGlowAnimation()
        {
            if (!enableFloatingGlow || _floatingGlowRoot == null || _haloRenderer == null) return;

            float t = Time.time;

            // Serene, soft breathing pulse
            float breathe = 1.0f + Mathf.Sin(t * 2.2f) * 0.08f;

            // Subtle bob response
            float bobVel = Mathf.Cos(_hoverTimer * Mathf.PI * 2f);
            float bobScale = 1.0f + bobVel * 0.10f;

            // Happy celebration expansion
            float celebrationBurst = _isCelebrating ? Mathf.Abs(Mathf.Sin(_celebrationTimer * Mathf.PI * 2.5f)) * 0.22f : 0f;

            float currentDiameter = haloDiameter * (breathe * bobScale + celebrationBurst);
            _haloRenderer.transform.localScale = new Vector3(currentDiameter, currentDiameter, 1f);

            if (_softGlowLight != null)
            {
                _softGlowLight.intensity = softLightIntensity * (breathe + celebrationBurst * 0.4f);
            }
        }

        #endregion

        #region Public API & Helper Methods

        /// <summary>
        /// Sets the bot's facial expression, with an optional auto-reset duration back to Idle.
        /// </summary>
        public void SetExpression(BotExpression expression, float resetAfterSeconds = 0f)
        {
            currentExpression = expression;

            if (_expressionResetCoroutine != null) StopCoroutine(_expressionResetCoroutine);

            if (resetAfterSeconds > 0f)
            {
                _expressionResetCoroutine = StartCoroutine(ResetExpressionRoutine(resetAfterSeconds));
            }
        }

        private IEnumerator ResetExpressionRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            currentExpression = BotExpression.Idle;
            _isCelebrating = false;
        }

        /// <summary>
        /// Triggers a joyous celebratory reaction (Happy eyes ^ ^, wide smile, cheerful hop, and chirp).
        /// Call this when a student places a planet correctly or completes a puzzle step!
        /// </summary>
        public void Celebrate(float duration = 2.8f)
        {
            SetExpression(BotExpression.Happy, duration);
            _isCelebrating = true;
            _celebrationTimer = 0f;

            PlayChirp(ChirpType.Happy);
        }

        /// <summary>
        /// Displays guidance or an adaptive hint in the world-space speech bubble with typewriter text animation.
        /// </summary>
        public void ShowHint(string message, float duration = 5.0f, AudioClip voiceClip = null)
        {
            if (!enableSpeechBubble || _speechBubbleObj == null) return;

            if (_bubbleDismissCoroutine != null) StopCoroutine(_bubbleDismissCoroutine);
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);

            SetExpression(BotExpression.Talking, duration);

            if (voiceClip != null && audioSource != null)
            {
                audioSource.clip = voiceClip;
                audioSource.Play();
            }
            else
            {
                PlayChirp(ChirpType.Talking);
            }

            _typewriterCoroutine = StartCoroutine(TypewriterTextRoutine(message, duration));
        }

        private IEnumerator TypewriterTextRoutine(string fullText, float displayDuration)
        {
            if (_speechBubbleCanvasGroup != null) _speechBubbleCanvasGroup.alpha = 1f;
            if (_speechBubbleText != null) _speechBubbleText.text = "";

            float charDelay = 1.0f / Mathf.Max(typewriterSpeed, 1f);

            for (int i = 0; i <= fullText.Length; i++)
            {
                if (_speechBubbleText != null)
                {
                    _speechBubbleText.text = fullText.Substring(0, i);
                }
                yield return new WaitForSeconds(charDelay);
            }

            yield return new WaitForSeconds(displayDuration);

            // Fade out
            float fadeTime = 0.5f;
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                if (_speechBubbleCanvasGroup != null)
                {
                    _speechBubbleCanvasGroup.alpha = 1.0f - (t / fadeTime);
                }
                yield return null;
            }

            if (_speechBubbleCanvasGroup != null) _speechBubbleCanvasGroup.alpha = 0f;
            if (currentExpression == BotExpression.Talking)
            {
                SetExpression(BotExpression.Idle);
            }
        }

        /// <summary>
        /// Hides the speech bubble immediately.
        /// </summary>
        public void HideHint()
        {
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            if (_speechBubbleCanvasGroup != null) _speechBubbleCanvasGroup.alpha = 0f;
            if (currentExpression == BotExpression.Talking) SetExpression(BotExpression.Idle);
        }

        /// <summary>
        /// Switches follow side between right shoulder and left shoulder.
        /// </summary>
        public void SetShoulder(bool rightSide)
        {
            followRightShoulder = rightSide;
        }

        /// <summary>
        /// Triggers thinking expression (e.g. while evaluating student performance or waiting).
        /// </summary>
        public void ExpressThinking(float duration = 2.5f)
        {
            SetExpression(BotExpression.Thinking, duration);
            PlayChirp(ChirpType.Thinking);
        }

        #endregion

        #region Audio & Procedural Chirps Synthesizer

        /// <summary>
        /// Plays an audio chirp corresponding to the specified event.
        /// If clips are not provided, procedurally synthesizes cute R2-D2 / Wall-E style FM beeps!
        /// </summary>
        public void PlayChirp(ChirpType type)
        {
            if (audioSource == null) return;

            AudioClip clipToPlay = null;

            if (type == ChirpType.Greeting && greetingAudioClip != null) clipToPlay = greetingAudioClip;
            else if (type == ChirpType.Happy && celebrationAudioClip != null) clipToPlay = celebrationAudioClip;

            if (clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay, chirpVolume);
            }
            else if (generateProceduralChirps)
            {
                AudioClip synthClip = GenerateChirpClip(type);
                if (synthClip != null)
                {
                    audioSource.PlayOneShot(synthClip, chirpVolume);
                }
            }
        }

        private AudioClip GenerateChirpClip(ChirpType type)
        {
            int sampleRate = 22050;
            float duration = 0.28f;
            float baseFreq = 750f;
            float modFreq = 18f;
            float modDepth = 280f;

            switch (type)
            {
                case ChirpType.Greeting:
                    duration = 0.24f;
                    baseFreq = 880f;
                    modFreq = 12f;
                    break;
                case ChirpType.Happy:
                    duration = 0.35f;
                    baseFreq = 1100f;
                    modFreq = 26f;
                    modDepth = 420f;
                    break;
                case ChirpType.Talking:
                    duration = 0.16f;
                    baseFreq = 620f;
                    modFreq = 8f;
                    break;
                case ChirpType.Thinking:
                    duration = 0.30f;
                    baseFreq = 540f;
                    modFreq = 5f;
                    break;
            }

            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Envelope (smooth attack and release)
                float envelope = Mathf.Sin((t / duration) * Mathf.PI);

                // Frequency modulation
                float currentFreq = baseFreq + Mathf.Sin(t * modFreq * Mathf.PI * 2f) * modDepth;
                // Gentle rising inflection for happy/greeting
                if (type == ChirpType.Happy || type == ChirpType.Greeting)
                {
                    currentFreq += (t / duration) * 350f;
                }

                samples[i] = Mathf.Sin(t * currentFreq * Mathf.PI * 2f) * envelope * 0.45f;
            }

            AudioClip clip = AudioClip.Create("Chirp_" + type, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion

        #region Editor Gizmos

        private void OnDrawGizmosSelected()
        {
            // Draw shoulder target position in editor
            Transform target = followTarget != null ? followTarget : (Camera.main != null ? Camera.main.transform : null);
            if (target != null)
            {
                Vector3 sideOffset = shoulderOffset;
                if (!followRightShoulder) sideOffset.x = -sideOffset.x;
                Vector3 idealPos = target.position + Quaternion.Euler(0f, target.eulerAngles.y, 0f) * sideOffset;

                Gizmos.color = new Color(0f, 0.9f, 1f, 0.4f);
                Gizmos.DrawSphere(idealPos, 0.08f);
                Gizmos.DrawLine(target.position, idealPos);

                // Draw min comfort buffer sphere
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(target.position, minComfortDistance);
            }
        }

        #endregion
    }
}
