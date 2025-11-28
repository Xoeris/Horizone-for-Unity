using UnityEngine;
// REQUIRED: Install Input System package from Package Manager
using UnityEngine.InputSystem;
using System.Collections.Generic; // Added for Dictionary

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Horizone
{
    [AddComponentMenu("Horizone/Horizone Camera")]
    // Removed RequireComponent to manage hiding/deleting manually
    [DisallowMultipleComponent] 
    public class HorizoneCamera : MonoBehaviour
    {
        public enum CameraMode { ThirdPerson, FirstPerson, Isometric3D, Isometric2D }

        [Header("Core Settings")]
        public Transform target;
        public CameraMode currentMode = CameraMode.ThirdPerson;
        public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

        [Header("Input Settings")]
        public InputActionReference lookAction; // Value (Vector2) - Mouse Delta or Right Stick
        public InputActionReference zoomAction; // Value (Vector2) - Scroll Wheel

        [Header("Control Settings")]
        public bool invertY = false;
        public bool lockCursor = true;

        [Header("Orbit (TPS/FPS) Settings")]
        public float lookSensitivity = 1.0f; // Mouse Sensitivity
        public float gamepadLookSensitivity = 150.0f; // Gamepad Sensitivity (Degrees per second)
        public float scrollSensitivity = 0.1f; 
        public float rotationSmoothTime = 0.12f; // GTA V Style Smoothing
        public float yMinLimit = -40f;
        public float yMaxLimit = 80f;
        
        // TPS Specifics
        public float distance = 5.0f;
        public float minDistance = 1.5f;
        public float maxDistance = 10.0f;
        public float smoothTime = 0.12f; // Position Smooth Time
        
        // FPS Specifics
        [Tooltip("Distance to step forward from the target offset in FPS mode")]
        public float fpsForwardOffset = 0.5f; 
        [Tooltip("If true, the character's body renderers will be set to 'Shadows Only' in First Person mode.")]
        public bool hideBodyInFPS = true; 

        [Header("Isometric Settings")]
        public float isoDistance = 10.0f;
        public float isoHeight = 10.0f; // Note: Used for offset calculation in logic
        public float isoAngle = 45.0f;
        public float isoSmoothSpeed = 5.0f; // Kept for reference, but static follow is now default
        [Tooltip("Only used in Isometric 2D mode")]
        public float orthographicSize = 5.0f;

        // Private runtime variables
        private float currentX = 0.0f;
        private float currentY = 0.0f;
        private float targetX = 0.0f; // Target Rotation X
        private float targetY = 0.0f; // Target Rotation Y
        private float xVelocity = 0.0f; // Smoothing Velocity
        private float yVelocity = 0.0f; // Smoothing Velocity
        private float currentDistance;
        private Vector3 currentVelocity = Vector3.zero;
        private Camera cam;
        
        // Constants
        private const float MOUSE_SENSITIVITY_MULTIPLIER = 0.1f; // Tames the raw mouse delta

        // Visibility Caching
        private Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> initialShadowModes = new Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>();
        private bool isBodyHidden = false;

        // Called when component is added or reset in inspector
        private void Reset()
        {
            EnsureHiddenCameraExists();
        }

        // Called when component is destroyed
        private void OnDestroy()
        {
            // If in editor and not playing, try to clean up the hidden camera buddy
            if (!Application.isPlaying && Application.isEditor)
            {
                cam = GetComponent<Camera>();
                // Only delete it if it's hidden (belongs to us)
                if (cam != null && (cam.hideFlags & HideFlags.HideInInspector) != 0)
                {
                    DestroyImmediate(cam);
                }
            }
        }

        private void OnEnable()
        {
            if (lookAction != null && lookAction.action != null) lookAction.action.Enable();
            if (zoomAction != null && zoomAction.action != null) zoomAction.action.Enable();
        }

        private void OnDisable()
        {
            if (lookAction != null && lookAction.action != null) lookAction.action.Disable();
            if (zoomAction != null && zoomAction.action != null) zoomAction.action.Disable();
        }

        public void EnsureHiddenCameraExists()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = gameObject.AddComponent<Camera>();
            }
            // Hide it in the inspector so it doesn't clutter the view
            cam.hideFlags = HideFlags.HideInInspector;
        }

        private void Start()
        {
            EnsureHiddenCameraExists();
            currentDistance = distance;

            // Initialize angles based on current rotation
            Vector3 angles = transform.eulerAngles;
            targetX = currentX = angles.y;
            targetY = currentY = angles.x;

            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Cache Renderers for Visibility toggling
            if (target != null)
            {
                Renderer[] rends = target.GetComponentsInChildren<Renderer>();
                foreach (var r in rends)
                {
                    initialShadowModes[r] = r.shadowCastingMode;
                }
            }
        }

        private void LateUpdate()
        {
            if (!target || !cam) return;

            // Handle Body Visibility (FPS Mode)
            ManageBodyVisibility();

            // Handle Projection Switching
            if (currentMode == CameraMode.Isometric2D)
            {
                if (!cam.orthographic) cam.orthographic = true;
                cam.orthographicSize = orthographicSize;
            }
            else
            {
                if (cam.orthographic) cam.orthographic = false;
            }

            // State Machine
            switch (currentMode)
            {
                case CameraMode.ThirdPerson:
                    HandleTPS();
                    break;
                case CameraMode.FirstPerson:
                    HandleFPS();
                    break;
                case CameraMode.Isometric3D:
                    HandleIso3D();
                    break;
                case CameraMode.Isometric2D:
                    HandleIso2D();
                    break;
            }
        }

        // --- Logic Methods ---

        void ManageBodyVisibility()
        {
            // Safety check: if renderers weren't cached yet (e.g. target assigned late), try to cache
            if (initialShadowModes.Count == 0 && target != null)
            {
                Renderer[] rends = target.GetComponentsInChildren<Renderer>();
                foreach (var r in rends) initialShadowModes[r] = r.shadowCastingMode;
            }

            bool shouldHide = (currentMode == CameraMode.FirstPerson && hideBodyInFPS);

            // If we need to hide but currently aren't hidden
            if (shouldHide && !isBodyHidden)
            {
                foreach (var kvp in initialShadowModes)
                {
                    if (kvp.Key != null) kvp.Key.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                isBodyHidden = true;
            }
            // If we need to show but currently are hidden
            else if (!shouldHide && isBodyHidden)
            {
                foreach (var kvp in initialShadowModes)
                {
                    if (kvp.Key != null) kvp.Key.shadowCastingMode = kvp.Value; // Restore original state
                }
                isBodyHidden = false;
            }
        }

        bool IsGamepadInput(InputActionReference actionRef)
        {
            if (actionRef == null || actionRef.action == null) return false;
            return actionRef.action.activeControl?.device is Gamepad;
        }

        void HandleTPS()
        {
            // Read Inputs
            Vector2 lookInput = Vector2.zero;
            float scrollInput = 0f;

            if (lookAction != null && lookAction.action != null)
                lookInput = lookAction.action.ReadValue<Vector2>();
            
            if (zoomAction != null && zoomAction.action != null)
                scrollInput = zoomAction.action.ReadValue<Vector2>().y;

            // Sensitivity Logic
            float finalSensX = lookSensitivity;
            float finalSensY = lookSensitivity;

            if (IsGamepadInput(lookAction))
            {
                // Gamepad: Rate-based
                finalSensX = gamepadLookSensitivity * Time.deltaTime;
                finalSensY = gamepadLookSensitivity * Time.deltaTime;
            }
            else
            {
                // Mouse: Multiply by a small factor to make 1.0 feel reasonable and smooth
                finalSensX *= MOUSE_SENSITIVITY_MULTIPLIER;
                finalSensY *= MOUSE_SENSITIVITY_MULTIPLIER;
            }

            // Update Target Angles
            targetX += lookInput.x * finalSensX;
            targetY -= lookInput.y * finalSensY * (invertY ? -1 : 1);
            targetY = Mathf.Clamp(targetY, yMinLimit, yMaxLimit);

            // Zoom Logic
            float scrollVal = Mathf.Clamp(scrollInput, -1f, 1f); 
            if(Mathf.Abs(scrollInput) > 0.01f) scrollVal = Mathf.Sign(scrollInput); 
            distance = Mathf.Clamp(distance - scrollVal * scrollSensitivity, minDistance, maxDistance);

            // Smoothly interpolate distance
            currentDistance = Mathf.Lerp(currentDistance, distance, Time.deltaTime * 10f);

            // Smoothly interpolate Rotation (GTA V Feel)
            currentX = Mathf.SmoothDamp(currentX, targetX, ref xVelocity, rotationSmoothTime);
            currentY = Mathf.SmoothDamp(currentY, targetY, ref yVelocity, rotationSmoothTime);

            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
            Vector3 negDistance = new Vector3(0.0f, 0.0f, -currentDistance);
            
            // Calculate position with offset
            Vector3 position = rotation * negDistance + (target.position + targetOffset);

            transform.rotation = rotation;
            transform.position = Vector3.SmoothDamp(transform.position, position, ref currentVelocity, smoothTime);
        }

        void HandleFPS()
        {
            Vector2 lookInput = Vector2.zero;
            if (lookAction != null && lookAction.action != null)
                lookInput = lookAction.action.ReadValue<Vector2>();

            // Sensitivity Logic
            float finalSensX = lookSensitivity;
            float finalSensY = lookSensitivity;

            if (IsGamepadInput(lookAction))
            {
                finalSensX = gamepadLookSensitivity * Time.deltaTime;
                finalSensY = gamepadLookSensitivity * Time.deltaTime;
            }
            else
            {
                 // Mouse: Multiply by a small factor to make 1.0 feel reasonable and smooth
                finalSensX *= MOUSE_SENSITIVITY_MULTIPLIER;
                finalSensY *= MOUSE_SENSITIVITY_MULTIPLIER;
            }

            targetX += lookInput.x * finalSensX;
            targetY -= lookInput.y * finalSensY * (invertY ? -1 : 1);
            targetY = Mathf.Clamp(targetY, -85f, 85f);

            // Smooth Rotation
            currentX = Mathf.SmoothDamp(currentX, targetX, ref xVelocity, rotationSmoothTime);
            currentY = Mathf.SmoothDamp(currentY, targetY, ref yVelocity, rotationSmoothTime);

            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
            
            // Forward step logic
            Vector3 forwardStep = rotation * Vector3.forward * fpsForwardOffset;
            Vector3 finalPos = target.position + targetOffset + forwardStep;

            transform.rotation = rotation;
            transform.position = finalPos;
        }

        void HandleIso3D()
        {
            // Perspective Isometric - Static Follow (No Skew/Pinch)
            
            // 1. Calculate Rigid Position (No smoothing to avoid lag causing perspective skew)
            Vector3 targetPos = target.position + targetOffset;

            // Calculate standard Offset based on Angle
            float zOffset = -isoDistance * Mathf.Cos(isoAngle * Mathf.Deg2Rad);
            float yOffset = isoDistance * Mathf.Sin(isoAngle * Mathf.Deg2Rad);

            // Set final position directly
            Vector3 desiredPos = new Vector3(targetPos.x, targetPos.y + yOffset, targetPos.z + zOffset);
            transform.position = desiredPos;

            // 2. Set Fixed Rotation (Do NOT LookAt target)
            // Fix rotation to isoAngle pitch, 0 yaw. 
            // This ensures the camera angle never changes relative to the player, eliminating distortion.
            transform.rotation = Quaternion.Euler(isoAngle, 0f, 0f);
        }

        void HandleIso2D()
        {
            // Orthographic Isometric - Static Follow
            Vector3 targetPos = target.position + targetOffset;

            // Standard isometric 45-degree angle offset logic
            Vector3 isoOffset = new Vector3(-10f, 10f, -10f).normalized * isoDistance; 
            
            // Static Follow: Direct assignment
            transform.position = targetPos + isoOffset;
            
            // Fixed Rotation (Look at target position ONCE relative to offset, or just fix the rotation)
            // For Orthographic, LookAt is usually fine IF the position is static, but setting explicit rotation is safer.
            // Standard Isometric rotation is approx (35.264, 45, 0)
            transform.rotation = Quaternion.LookRotation(-isoOffset); 
        }
    }

    // ---------------------------------------------------------
    // CUSTOM INSPECTOR (EDITOR CODE)
    // ---------------------------------------------------------
#if UNITY_EDITOR
    [CustomEditor(typeof(HorizoneCamera))]
    public class HorizoneCameraEditor : Editor
    {
        HorizoneCamera targetScript;
        const string ICON_PATH = "Assets/Horizone/HorizoneEngine/Camera/HorizoneCamera.png";

        SerializedProperty targetProp;
        SerializedProperty modeProp;
        SerializedProperty offsetProp;

        // Input
        SerializedProperty lookAct;
        SerializedProperty zoomAct;
        SerializedProperty invert;
        SerializedProperty lockCur;

        // Orbit
        SerializedProperty lookSens;
        SerializedProperty gamepadSens;
        SerializedProperty scrollSens;
        SerializedProperty rotSmooth;
        SerializedProperty yMin;
        SerializedProperty yMax;
        SerializedProperty dist;
        SerializedProperty minD;
        SerializedProperty maxD;
        SerializedProperty smooth;
        SerializedProperty fpsOffset; 
        SerializedProperty hideBodyFPS; 

        // Iso
        SerializedProperty isoDist;
        SerializedProperty isoAng;
        SerializedProperty isoSmooth;
        SerializedProperty orthoSize;

        // Cached Editor for the actual Camera Component
        // This will automatically draw the correct inspector for URP, HDRP, or Built-in
        Editor cameraEditor; 

        // NEW: Add static constructor to register hierarchy callback
        [InitializeOnLoadMethod]
        static void InitHierarchyIcon()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= DrawHierarchyIcon;
            EditorApplication.hierarchyWindowItemOnGUI += DrawHierarchyIcon;
        }

        static void DrawHierarchyIcon(int instanceID, Rect selectionRect)
        {
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null) return;

            if (gameObject.GetComponent<HorizoneCamera>() != null)
            {
                // Draw icon on the right
                Rect iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                
                Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
                // Fallback to default Camera icon if custom one is missing
                Texture icon = (customIcon != null) ? customIcon : EditorGUIUtility.IconContent("Camera Icon").image;

                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                }
            }
        }

        // --- MENU ITEM ADDITION ---
        [MenuItem("GameObject/Horizone/Horizone Camera", false, 10)]
        static void CreateHorizoneCamera(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Horizone Camera");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            go.AddComponent<HorizoneCamera>();
            Selection.activeObject = go;

            // Apply GameObject Icon immediately
            Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
            if (customIcon != null)
            {
                EditorGUIUtility.SetIconForObject(go, customIcon);
            }
        }

        private void OnEnable()
        {
            targetScript = (HorizoneCamera)target;
            // Ensure camera exists before we try to create a SerializedObject for it
            targetScript.EnsureHiddenCameraExists();

            // Apply GameObject Icon when inspector is opened (ensures existing objects get the icon)
            Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
            if (customIcon != null)
            {
                EditorGUIUtility.SetIconForObject(targetScript.gameObject, customIcon);
            }

            targetProp = serializedObject.FindProperty("target");
            modeProp = serializedObject.FindProperty("currentMode");
            offsetProp = serializedObject.FindProperty("targetOffset");

            lookAct = serializedObject.FindProperty("lookAction");
            zoomAct = serializedObject.FindProperty("zoomAction");
            invert = serializedObject.FindProperty("invertY");
            lockCur = serializedObject.FindProperty("lockCursor");

            lookSens = serializedObject.FindProperty("lookSensitivity");
            gamepadSens = serializedObject.FindProperty("gamepadLookSensitivity");
            scrollSens = serializedObject.FindProperty("scrollSensitivity");
            rotSmooth = serializedObject.FindProperty("rotationSmoothTime");
            yMin = serializedObject.FindProperty("yMinLimit");
            yMax = serializedObject.FindProperty("yMaxLimit");
            dist = serializedObject.FindProperty("distance");
            minD = serializedObject.FindProperty("minDistance");
            maxD = serializedObject.FindProperty("maxDistance");
            smooth = serializedObject.FindProperty("smoothTime");
            fpsOffset = serializedObject.FindProperty("fpsForwardOffset");
            hideBodyFPS = serializedObject.FindProperty("hideBodyInFPS"); 

            isoDist = serializedObject.FindProperty("isoDistance");
            isoAng = serializedObject.FindProperty("isoAngle");
            isoSmooth = serializedObject.FindProperty("isoSmoothSpeed");
            orthoSize = serializedObject.FindProperty("orthographicSize");

            // Initialize Camera Editor
            Camera hiddenCam = targetScript.GetComponent<Camera>();
            if (hiddenCam != null)
            {
                // Destroy previous editor if it exists to avoid leaks
                if (cameraEditor != null) DestroyImmediate(cameraEditor);
                // Create a standard editor for the camera component
                // This automagically respects the current Render Pipeline (HDRP/URP/Built-in)
                cameraEditor = Editor.CreateEditor(hiddenCam);
            }
        }

        private void OnDisable()
        {
            // Clean up the cached editor to prevent memory leaks
            if (cameraEditor != null) DestroyImmediate(cameraEditor);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- HEADER ---
            DrawHeader();

            EditorGUILayout.Space(10);

            // --- CORE ---
            EditorGUILayout.LabelField("Core Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetProp, new GUIContent("Target", "Transform to follow."));
            EditorGUILayout.PropertyField(offsetProp, new GUIContent("Target Offset", "Offset position relative to target."));
            
            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
            EditorGUILayout.PropertyField(modeProp, new GUIContent("Camera Mode", "Select camera behavior mode."));
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            HorizoneCamera.CameraMode mode = (HorizoneCamera.CameraMode)modeProp.enumValueIndex;

            // --- DYNAMIC SETTINGS BASED ON MODE ---
            
            if (mode == HorizoneCamera.CameraMode.ThirdPerson || mode == HorizoneCamera.CameraMode.FirstPerson)
            {
                DrawInputSettings();
                DrawOrbitSettings(mode == HorizoneCamera.CameraMode.FirstPerson);
            }
            else if (mode == HorizoneCamera.CameraMode.Isometric3D || mode == HorizoneCamera.CameraMode.Isometric2D)
            {
                DrawIsoSettings(mode == HorizoneCamera.CameraMode.Isometric2D);
            }

            EditorGUILayout.Space(10);
            // --- EMBEDDED CAMERA SETTINGS ---
            DrawEmbeddedCameraSettings();

            serializedObject.ApplyModifiedProperties();
        }

        new void DrawHeader()
        {
            // Reserve space for the header to draw into
            GUILayout.Space(10);
            Rect headerRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));

            // Safety check: Don't draw if rect is invalid (happens during initialization)
            if (headerRect.width <= 1) return;

            // --- Background & Border ---
            if (Event.current.type == EventType.Repaint)
            {
                // Draw rounded background
                GUI.DrawTexture(headerRect, MakeRoundedTex((int)headerRect.width, (int)headerRect.height, new Color(0.1f, 0.1f, 0.1f, 0.9f)));

                // Draw border manually using single-pixel lines
                Color borderColor = new Color(0.196f, 0.945f, 0.541f, 1.0f);
                GUI.DrawTexture(new Rect(headerRect.x, headerRect.y, headerRect.width, 2), MakeTex((int)headerRect.width, 2, borderColor)); // Top
                GUI.DrawTexture(new Rect(headerRect.x, headerRect.yMax - 2, headerRect.width, 2), MakeTex((int)headerRect.width, 2, borderColor)); // Bottom
                GUI.DrawTexture(new Rect(headerRect.x, headerRect.y, 2, headerRect.height), MakeTex(2, (int)headerRect.height, borderColor)); // Left
                GUI.DrawTexture(new Rect(headerRect.xMax - 2, headerRect.y, 2, headerRect.height), MakeTex(2, (int)headerRect.height, borderColor)); // Right
            }

            // --- Content (Manual GUI Positioning) ---
            
            // 1. Draw Icon
            Rect iconRect = new Rect(headerRect.x + 10, headerRect.y + 10, 40, 40);
            Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
            Texture iconTex = (customIcon != null) ? customIcon : EditorGUIUtility.IconContent("Camera Icon").image;
            
            if (iconTex != null)
            {
                GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit);
            }

            // 2. Draw Title
            Rect titleRect = new Rect(iconRect.xMax + 10, headerRect.y + 10, headerRect.width - 60, 20);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 16;
            titleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f); // Bright text for dark background
            GUI.Label(titleRect, "HORIZONE CAMERA", titleStyle);

            // 3. Draw Subtitle
            Rect subTitleRect = new Rect(iconRect.xMax + 10, headerRect.y + 30, headerRect.width - 60, 20);
            GUI.Label(subTitleRect, "Horizone Powerful Advanced Camera System", EditorStyles.miniLabel);
        }

        // Helper to create rounded texture
        private Texture2D MakeRoundedTex(int width, int height, Color col)
        {
            if (width <= 0 || height <= 0) return null;

            Texture2D result = new Texture2D(width, height);
            Color[] pix = new Color[width * height];
            
            // Corner radius
            int radius = 10;
            // Limit radius to half the smallest dimension to prevent overlap artifacts
            int minDim = Mathf.Min(width, height);
            if (radius * 2 > minDim) radius = minDim / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Normalize coordinates to a single corner
                    int px = x < width / 2 ? x : width - 1 - x;
                    int py = y < height / 2 ? y : height - 1 - y;

                    float alpha = 1f;

                    // If we are inside the corner square region
                    if (px < radius && py < radius)
                    {
                        float dx = radius - px;
                        float dy = radius - py;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        // Corner out logic (Convex)
                        if (dist > radius) 
                            alpha = 0f;
                        else if (dist > radius - 1f) 
                            alpha = 1f - (dist - (radius - 1f));
                        else 
                            alpha = 1f;
                    }
                    
                    pix[y * width + x] = new Color(col.r, col.g, col.b, col.a * alpha);
                }
            }
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        // Helper method to create a texture for the background
        private Texture2D MakeTex(int width, int height, Color col)
        {
            if (width <= 0 || height <= 0) return null;

            Color[] pix = new Color[width * height];
            for(int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        void DrawInputSettings()
        {
            EditorGUILayout.LabelField("Input & Control", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(lookAct, new GUIContent("Look Action (Vector2)", "Input action for looking around."));
            EditorGUILayout.PropertyField(zoomAct, new GUIContent("Zoom Action (Scroll)", "Input action for zooming in/out."));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(invert, new GUIContent("Invert Y", "Invert vertical look axis."));
            EditorGUILayout.PropertyField(lockCur, new GUIContent("Lock Cursor", "Lock and hide cursor on start."));
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        void DrawOrbitSettings(bool isFPS)
        {
            EditorGUILayout.LabelField("Rotation & Zoom", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.PropertyField(lookSens, new GUIContent("Mouse Sensitivity", "Sensitivity for mouse input."));
            EditorGUILayout.PropertyField(gamepadSens, new GUIContent("Gamepad Sensitivity", "Sensitivity for gamepad input."));
            EditorGUILayout.PropertyField(rotSmooth, new GUIContent("Look Smoothing", "Smoothing factor for camera rotation."));
            
            if (isFPS)
            {
                EditorGUILayout.PropertyField(fpsOffset, new GUIContent("FPS Forward Step", "Forward offset in First Person mode."));
                EditorGUILayout.PropertyField(hideBodyFPS, new GUIContent("Hide Body in FPS", "Hide character mesh in First Person mode."));
            }
            else
            {
                EditorGUILayout.PropertyField(scrollSens, new GUIContent("Scroll Sensitivity", "Sensitivity for zoom scrolling."));
                EditorGUILayout.PropertyField(dist, new GUIContent("Distance", "Current distance from target."));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Zoom Limits");
                EditorGUILayout.PropertyField(minD, GUIContent.none);
                EditorGUILayout.PropertyField(maxD, GUIContent.none);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(smooth, new GUIContent("Follow Smoothing", "Smoothing factor for camera position follow."));
            }

            EditorGUILayout.LabelField("Vertical Angle Limits");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(yMin.floatValue.ToString("0"), GUILayout.Width(30));
            
            float minVal = yMin.floatValue;
            float maxVal = yMax.floatValue;
            EditorGUILayout.MinMaxSlider(ref minVal, ref maxVal, -90f, 90f);
            yMin.floatValue = minVal;
            yMax.floatValue = maxVal;

            EditorGUILayout.LabelField(yMax.floatValue.ToString("0"), GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DrawIsoSettings(bool is2D)
        {
            EditorGUILayout.LabelField("Isometric Parameters", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (is2D)
            {
                EditorGUILayout.PropertyField(orthoSize, new GUIContent("Orthographic Size", "Size of the orthographic camera view."));
                EditorGUILayout.HelpBox("Camera is now in Orthographic Mode.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(isoAng, new GUIContent("Isometric Angle", "Vertical angle for isometric view."));
                EditorGUILayout.HelpBox("Camera is in Perspective Mode.", MessageType.Info);
            }

            EditorGUILayout.PropertyField(isoDist, new GUIContent("Iso Distance", "Distance for isometric camera."));
            EditorGUILayout.PropertyField(isoSmooth, new GUIContent("Iso Smoothing", "Smoothing speed for isometric follow."));
            
            EditorGUILayout.EndVertical();
        }

        void DrawEmbeddedCameraSettings()
        {
            // If for some reason the editor isn't created, try to create it again (resilience)
            if (cameraEditor == null && targetScript != null)
            {
                Camera hiddenCam = targetScript.GetComponent<Camera>();
                if (hiddenCam != null) cameraEditor = Editor.CreateEditor(hiddenCam);
            }

            if (cameraEditor != null)
            {
                EditorGUILayout.LabelField("Internal Camera Settings", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                // This draws the standard inspector for the camera, matching whatever RP is active
                cameraEditor.OnInspectorGUI();
                EditorGUILayout.EndVertical();
            }
        }
    }
#endif
}