using GorillaLocomotion;
using UnityEngine;
using UnityEngine.XR;
using static iiMenu.Menu.Main;

namespace iiMenu.Managers
{
    public static class ThrowableMenuManager
    {
        private enum DiscState
        {
            Hidden,
            Held,
            Flying,
            Deploying,
            Deployed
        }

        private static readonly Color MainOrange = new Color(1f, 0.30f, 0.015f, 1f);
        private static readonly Color SoftOrange = new Color(1f, 0.52f, 0.06f, 1f);
        private static readonly Color GlowOrange = new Color(1f, 0.20f, 0.005f, 1f);
        private static readonly Vector3[] PositionSamples = new Vector3[8];
        private static readonly float[] SampleTimes = new float[8];

        private static GameObject discObject;
        private static Transform discTransform;
        private static Transform heldHand;
        private static TrailRenderer trail;
        private static LineRenderer[] rings;
        private static GameObject ringRoot;
        private static DiscState state;
        private static int sampleIndex;
        private static bool lastButtonHeld;
        private static bool selectedHandIsRight;
        private static float flightProgress;
        private static float deployProgress;
        private static float arrivalTime;
        private static Vector3 flightStart;
        private static Vector3 flightControl;
        private static Vector3 flightTarget;
        private static Quaternion deployStartRotation;
        private static Vector3 menuBaseScale;

        public static bool IsDeployed => state == DiscState.Deployed && discObject != null && menu != null;

        public static void Cleanup()
        {
            try
            {
                if (ringRoot != null)
                    Object.Destroy(ringRoot);
            }
            catch { }

            try
            {
                if (discObject != null)
                    Object.Destroy(discObject);
            }
            catch { }

            discObject = null;
            discTransform = null;
            heldHand = null;
            trail = null;
            rings = null;
            ringRoot = null;
            state = DiscState.Hidden;
            followOffset = Vector3.zero;
            followTargetPosition = Vector3.zero;
            followMoveVelocity = Vector3.zero;
            sampleIndex = 0;
            lastButtonHeld = false;
            flightProgress = 0f;
            deployProgress = 0f;
            arrivalTime = 0f;
            menuBaseScale = Vector3.zero;
        }

        public static void OnMenuClosed()
        {
            if (throwableMenu)
                Cleanup();
        }

        private static Transform GetHand(bool rightHandSide)
        {
            try
            {
                return rightHandSide
                    ? GorillaTagger.Instance.rightHandTransform
                    : GorillaTagger.Instance.leftHandTransform;
            }
            catch
            {
                return null;
            }
        }

        private static Vector3 PalmNormal(Transform hand, bool leftHand)
        {
            return leftHand ? hand.right : -hand.right;
        }

        private static Quaternion PalmRotation(Transform hand, bool leftHand)
        {
            Vector3 normal = PalmNormal(hand, leftHand);
            Vector3 up = hand.rotation * Quaternion.Euler(45f, 0f, 0f) * Vector3.forward;
            return Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.FromToRotation(Vector3.forward, up);
        }

        private const float FollowDistance = 0.62f;
        private const float MinimumFollowDistance = 0.34f;
        private const float MaximumFollowDistance = 1.05f;
        private const float FollowMoveSmoothTime = 0.18f;
        private const float GestureMinimumSpeed = 0.55f;
        private static Vector3 followOffset;
        private static Vector3 followTargetPosition;
        private static Vector3 followMoveVelocity;
        private static float nextGestureTime;

        private static Transform GetHead()
        {
            try
            {
                return GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                    ? GorillaTagger.Instance.headCollider.transform
                    : Camera.main != null ? Camera.main.transform : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsStopHand(Transform head)
        {
            Transform left = GetHand(false);
            if (left == null)
                return false;

            Vector3 toHead = (head.position - left.position).normalized;
            Vector3 palmNormal = PalmNormal(left, true);
            Vector3 velocity = HandVelocity(false);
            bool palmFacingPlayer = Vector3.Dot(palmNormal, toHead) > 0.45f;
            bool handMostlyStill = velocity.magnitude < 0.45f;
            bool handInFront = Vector3.Distance(left.position, head.position) < 1.2f;
            return palmFacingPlayer && handMostlyStill && handInFront;
        }

        private static bool TryReadFollowGesture(Transform head, out Vector3 gesture)
        {
            gesture = Vector3.zero;
            if (!throwableMenuGestures || head == null || Time.time < nextGestureTime || !IsStopHand(head))
                return false;

            Vector3 velocity = HandVelocity(true);
            if (velocity.magnitude < GestureMinimumSpeed)
                return false;

            Vector3 localVelocity = head.InverseTransformDirection(velocity);
            float horizontal = Mathf.Abs(localVelocity.x);
            float vertical = Mathf.Abs(localVelocity.y);
            float depth = Mathf.Abs(localVelocity.z);

            if (depth >= horizontal && depth >= vertical)
                gesture = Vector3.forward * Mathf.Sign(localVelocity.z);
            else if (horizontal >= vertical)
                gesture = Vector3.right * Mathf.Sign(localVelocity.x);
            else
                gesture = Vector3.up * Mathf.Sign(localVelocity.y);

            if (gesture == Vector3.zero)
                return false;

            nextGestureTime = Time.time + 0.40f;
            return true;
        }

        private static void UpdateFollowTarget(Transform head)
        {
            if (!throwableFollowPlayer || head == null)
                return;

            if (followTargetPosition == Vector3.zero)
            {
                followOffset = Vector3.forward * FollowDistance;
                followTargetPosition = head.TransformPoint(followOffset);
                followMoveVelocity = Vector3.zero;
            }

            if (TryReadFollowGesture(head, out Vector3 gesture))
            {
                if (gesture == Vector3.forward)
                    followOffset.z = Mathf.Clamp(followOffset.z + 0.28f, MinimumFollowDistance, MaximumFollowDistance);
                else if (gesture == Vector3.back)
                    followOffset.z = Mathf.Clamp(followOffset.z - 0.28f, MinimumFollowDistance, MaximumFollowDistance);
                else if (gesture == Vector3.right)
                    followOffset.x = Mathf.Clamp(followOffset.x + 0.32f, -0.65f, 0.65f);
                else if (gesture == Vector3.left)
                    followOffset.x = Mathf.Clamp(followOffset.x - 0.32f, -0.65f, 0.65f);
                else if (gesture == Vector3.up)
                    followOffset.y = Mathf.Clamp(followOffset.y + 0.18f, -0.45f, 0.45f);
                else if (gesture == Vector3.down)
                    followOffset.y = Mathf.Clamp(followOffset.y - 0.18f, -0.45f, 0.45f);
            }

            followTargetPosition = head.TransformPoint(followOffset);
            if (flightTarget == Vector3.zero)
                flightTarget = followTargetPosition;
            flightTarget = Vector3.SmoothDamp(flightTarget, followTargetPosition, ref followMoveVelocity, FollowMoveSmoothTime, 3.5f, Time.deltaTime);
        }

        private static void FaceMenuToHead(Transform head)
        {
            menu.transform.position = flightTarget;
            menu.transform.LookAt(head.position);
            Vector3 rotation = menu.transform.rotation.eulerAngles;
            rotation += new Vector3(-90f, 0f, -90f);
            menu.transform.rotation = Quaternion.Euler(rotation);
        }

        private static void FaceDiscToHead(Transform disc, Transform head, float spin)
        {
            Vector3 toHead = head.position - disc.position;
            if (toHead.sqrMagnitude < 0.001f)
                return;

            Quaternion facePlayer = Quaternion.FromToRotation(Vector3.up, toHead.normalized);
            disc.rotation = facePlayer * Quaternion.AngleAxis(spin, Vector3.up);
        }

        private static Vector3 BaseDiscScale => new Vector3(0.13f, 0.006f, 0.13f);

        private static Vector3 HandVelocity(bool rightHandSide)
        {
            try
            {
                if (GTPlayer.Instance != null)
                {
                    return rightHandSide
                        ? GTPlayer.Instance.RightHand.velocityTracker.GetAverageVelocity(true, 0)
                        : GTPlayer.Instance.LeftHand.velocityTracker.GetAverageVelocity(true, 0);
                }
            }
            catch { }

            return Vector3.zero;
        }

        private static Material MakeMaterial(Color color, float emissionStrength = 0f)
        {
            Shader shader = Shader.Find("GUI/Text Shader");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color * (emissionStrength > 0f ? emissionStrength : 1f));
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color * (emissionStrength > 0f ? emissionStrength : 1f));
            return material;
        }

        private static void CreateDisc()
        {
            Cleanup();

            Transform hand = GetHand(selectedHandIsRight);
            if (hand == null)
                return;

            discObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            discObject.name = "iiMenu_ThrowableDisc";
            Object.Destroy(discObject.GetComponent<Collider>());
            Object.DontDestroyOnLoad(discObject);
            discTransform = discObject.transform;
            discTransform.localScale = BaseDiscScale;

            Renderer discRenderer = discObject.GetComponent<Renderer>();
            Material discMaterial = MakeMaterial(new Color(0.11f, 0.012f, 0.002f, 0.20f), 1f);
            if (discMaterial != null)
                discRenderer.material = discMaterial;

            ringRoot = new GameObject("iiMenu_ThrowableDiscRings");
            Object.DontDestroyOnLoad(ringRoot);
            ringRoot.transform.SetParent(discTransform, false);
            ringRoot.transform.localPosition = Vector3.zero;
            ringRoot.transform.localRotation = Quaternion.identity;

            rings = new[]
            {
                BuildArc("arcA", 0.34f, -0.15f, 1.45f, SoftOrange, 18),
                BuildArc("arcB", 0.43f, 1.35f, 1.70f, MainOrange, 20),
                BuildArc("arcC", 0.52f, 2.75f, 1.35f, new Color(1f, 0.64f, 0.12f, 0.85f), 18),
                BuildArc("arcD", 0.39f, 3.85f, 1.55f, GlowOrange, 18),
                BuildArc("arcE", 0.55f, 5.00f, 1.20f, SoftOrange, 16)
            };

            trail = discObject.AddComponent<TrailRenderer>();
            trail.time = 0.4f;
            trail.startWidth = 0.012f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.008f;
            trail.material = MakeMaterial(new Color(1f, 0.35f, 0.02f, 0.65f), 1f);
            trail.emitting = false;

            AttachToPalm(hand);
            state = DiscState.Held;
            sampleIndex = 0;
            for (int i = 0; i < PositionSamples.Length; i++)
            {
                PositionSamples[i] = discTransform.position;
                SampleTimes[i] = Time.time;
            }
        }

        private static LineRenderer BuildArc(string name, float radius, float startAngle, float span, Color color, int segments)
        {
            GameObject arcObject = new GameObject("iiMenu_Disc_" + name);
            arcObject.transform.SetParent(ringRoot.transform, false);
            arcObject.transform.localPosition = Vector3.zero;
            arcObject.transform.localRotation = Quaternion.identity;

            LineRenderer line = arcObject.AddComponent<LineRenderer>();
            line.material = MakeMaterial(color, 1f);
            line.startColor = color;
            line.endColor = color;
            line.startWidth = 0.012f;
            line.endWidth = 0.003f;
            line.useWorldSpace = false;
            line.loop = false;
            line.numCornerVertices = 8;
            line.numCapVertices = 8;
            line.positionCount = segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = startAngle + span * i / (segments - 1f);
                float radiusBreathe = radius + Mathf.Sin(i / (segments - 1f) * Mathf.PI) * 0.012f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radiusBreathe, 0f, Mathf.Sin(angle) * radiusBreathe));
            }

            return line;
        }

        private static void AttachToPalm(Transform hand)
        {
            if (hand == null || discTransform == null)
                return;

            heldHand = hand;
            discTransform.SetParent(hand, false);
            Vector3 palmOffset = PalmNormal(hand, !selectedHandIsRight) * 0.012f;
            discTransform.localPosition = hand.InverseTransformDirection(palmOffset);
            discTransform.localRotation = Quaternion.Inverse(hand.rotation) * PalmRotation(hand, !selectedHandIsRight);
            discTransform.localScale = BaseDiscScale;
        }

        private static void TrackHeldDisc()
        {
            Transform hand = GetHand(selectedHandIsRight);
            if (hand == null || discTransform == null)
                return;

            if (heldHand != hand)
                AttachToPalm(hand);

            discTransform.SetParent(hand, false);
            Vector3 palmOffset = PalmNormal(hand, !selectedHandIsRight) * 0.012f;
            discTransform.localPosition = hand.InverseTransformDirection(palmOffset);
            discTransform.localRotation = Quaternion.Inverse(hand.rotation) * PalmRotation(hand, !selectedHandIsRight) * Quaternion.AngleAxis(Time.time * 35f, Vector3.up);

            PositionSamples[sampleIndex] = hand.position;
            SampleTimes[sampleIndex] = Time.time;
            sampleIndex = (sampleIndex + 1) % PositionSamples.Length;

            float heldPulse = 0.94f + Mathf.Sin(Time.time * 2.2f) * 0.06f;
            discTransform.localScale = BaseDiscScale * heldPulse;
            PulseRings();
        }

        private static Vector3 EstimateThrowVelocity()
        {
            int newest = (sampleIndex + PositionSamples.Length - 1) % PositionSamples.Length;
            int oldest = sampleIndex;
            float elapsed = SampleTimes[newest] - SampleTimes[oldest];
            if (elapsed < 0.01f)
                return Vector3.zero;

            return (PositionSamples[newest] - PositionSamples[oldest]) / elapsed;
        }

        private static void BeginFlight()
        {
            Vector3 velocity = EstimateThrowVelocity();
            Vector3 trackerVelocity = HandVelocity(selectedHandIsRight);
            if (trackerVelocity.magnitude > velocity.magnitude)
                velocity = trackerVelocity;

            Transform head = GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                ? GorillaTagger.Instance.headCollider.transform
                : Camera.main != null ? Camera.main.transform : null;
            if (head == null)
                return;

            if (velocity.magnitude < 0.25f)
                velocity = head.forward * 2f;

            flightStart = discTransform.position;
            flightTarget = head.position + head.forward * 1.30f;
            flightTarget.y = head.position.y - 0.02f;
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();
            flightControl = (flightStart + flightTarget) * 0.5f + Vector3.up * 0.30f + forward * Mathf.Clamp(velocity.magnitude * 0.04f, 0f, 0.18f);

            discTransform.SetParent(null, true);
            state = DiscState.Flying;
            flightProgress = 0f;
            trail.Clear();
            trail.emitting = true;
        }

        private static void UpdateFlight()
        {
            flightProgress += Time.deltaTime / 0.50f;
            float progress = Mathf.Clamp01(flightProgress);
            float eased = progress * progress * (3f - 2f * progress);
            discTransform.position = Vector3.Lerp(
                Vector3.Lerp(flightStart, flightControl, eased),
                Vector3.Lerp(flightControl, flightTarget, eased), eased);

            Transform head = GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                ? GorillaTagger.Instance.headCollider.transform
                : Camera.main != null ? Camera.main.transform : null;
            if (head != null)
                FaceDiscToHead(discTransform, head, Time.time * 900f * (1f - progress * 0.55f));
            PulseRings();

            if (progress >= 1f)
            {
                state = DiscState.Deploying;
                deployProgress = 0f;
                deployStartRotation = discTransform.rotation;
                trail.emitting = false;
            }
        }

        private static void StartMenuDeployment()
        {
            Transform head = GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                ? GorillaTagger.Instance.headCollider.transform
                : Camera.main != null ? Camera.main.transform : null;
            if (head == null)
                return;

            discTransform.position = flightTarget;
            discTransform.rotation = Quaternion.LookRotation(head.position - flightTarget, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            discObject.SetActive(false);
            ringRoot.transform.SetParent(null, true);
            ringRoot.transform.position = flightTarget;
            ringRoot.transform.localScale = Vector3.one * 0.08f;

            try
            {
                OpenMenu();
            }
            catch { }

            if (menu == null)
            {
                Cleanup();
                return;
            }

            menuBaseScale = menu.transform.localScale;
            followOffset = Vector3.forward * FollowDistance;
            followTargetPosition = head.TransformPoint(followOffset);
            followMoveVelocity = Vector3.zero;
            if (throwableFollowPlayer)
                UpdateFollowTarget(head);
            FaceMenuToHead(head);
            menu.transform.localScale = menuBaseScale * 0.035f;
            arrivalTime = Time.time;
            state = DiscState.Deployed;
        }

        private static void UpdateDeployment()
        {
            deployProgress += Time.deltaTime / 0.32f;
            float progress = Mathf.Clamp01(deployProgress);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            Transform head = GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                ? GorillaTagger.Instance.headCollider.transform
                : Camera.main != null ? Camera.main.transform : null;
            if (head != null)
            {
                Vector3 toHead = head.position - flightTarget;
                discTransform.rotation = Quaternion.Slerp(deployStartRotation, Quaternion.LookRotation(toHead, Vector3.up) * Quaternion.Euler(90f, 0f, 0f), eased);
            }
            discTransform.localScale = Vector3.Lerp(BaseDiscScale, BaseDiscScale * 1.25f, eased);
            PulseRings();

            if (progress >= 1f)
                StartMenuDeployment();
        }

        private static void UpdateDeployed()
        {
            if (menu == null || ringRoot == null)
                return;

            Transform head = GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                ? GorillaTagger.Instance.headCollider.transform
                : Camera.main != null ? Camera.main.transform : null;
            if (head == null)
                return;

            UpdateFollowTarget(head);
            FaceMenuToHead(head);

            float progress = Mathf.Clamp01((Time.time - arrivalTime) / 0.62f);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            float overshoot = Mathf.Sin(progress * Mathf.PI) * 0.08f;
            menu.transform.localScale = Vector3.Lerp(menuBaseScale * 0.035f, menuBaseScale, eased) * (1f + overshoot);
            ringRoot.transform.position = flightTarget;
            Quaternion ringRotation = Quaternion.FromToRotation(Vector3.up, (head.position - flightTarget).normalized);
            ringRoot.transform.rotation = ringRotation * Quaternion.AngleAxis(Time.time * 45f, Vector3.up);
            ringRoot.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 1f, eased);
            AnimateArcs();
            PulseRings();
        }

        private static void AnimateArcs()
        {
            if (rings == null)
                return;

            float direction = selectedHandIsRight ? -1f : 1f;
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null)
                    continue;

                float speed = (0.45f + i * 0.13f) * direction;
                float angle = Time.time * speed * 35f;
                float tilt = Mathf.Sin(Time.time * (1.2f + i * 0.15f)) * 8f;
                rings[i].transform.localRotation = Quaternion.Euler(tilt, angle, (i % 2 == 0 ? 1f : -1f) * 12f);
                rings[i].transform.localPosition = Vector3.up * (Mathf.Sin(Time.time * 1.5f + i) * 0.018f);
            }
        }

        private static void PulseRings()
        {
            if (rings == null)
                return;

            float pulse = (Mathf.Sin(Time.time * 3.2f) + 1f) * 0.5f;
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null)
                    continue;

                float alpha = i == 0 ? Mathf.Lerp(0.35f, 0.75f, pulse) : Mathf.Lerp(0.50f, 1f, 1f - pulse);
                Color color = i == 4 ? GlowOrange : i == 2 ? SoftOrange : MainOrange;
                color.a = alpha;
                rings[i].startColor = color;
                rings[i].endColor = color;
            }
        }

        public static bool Tick(bool buttonHeld, bool isKeyboardCondition)
        {
            bool pressed = buttonHeld && !lastButtonHeld;
            bool released = !buttonHeld && lastButtonHeld;
            lastButtonHeld = buttonHeld;

            if (!throwableMenu || isKeyboardCondition || !XRSettings.isDeviceActive)
            {
                if (discObject != null || ringRoot != null)
                    Cleanup();
                return false;
            }

            if (pressed && state == DiscState.Deployed)
            {
                Transform head = GetHead();
                    try { CloseMenu(); } catch { }
                Cleanup();
                return true;
            }

            if (pressed && state == DiscState.Hidden)
            {
                selectedHandIsRight = rightHand;
                CreateDisc();
                return true;
            }

            switch (state)
            {
                case DiscState.Held:
                    if (buttonHeld)
                        TrackHeldDisc();
                    else if (released)
                        BeginFlight();
                    return true;

                case DiscState.Flying:
                    UpdateFlight();
                    return true;

                case DiscState.Deploying:
                    UpdateDeployment();
                    return true;

                case DiscState.Deployed:
                    UpdateDeployed();
                    return true;
            }

            return false;
        }

        public static void OnRecenterMenu()
        {
        }
    }
}
