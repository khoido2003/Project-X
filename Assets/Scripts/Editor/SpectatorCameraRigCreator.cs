#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Editor utility to create the SpectatorCameraRig prefab.
/// Run from menu: Tools > Spectator > Create Spectator Camera Rig
/// </summary>
public class SpectatorCameraRigCreator : EditorWindow
{
    [MenuItem("Tools/Spectator/Create Spectator Camera Rig")]
    public static void CreateSpectatorCameraRig()
    {
        // Create root object
        GameObject root = new GameObject("SpectatorCameraRig");

        // Add SpectatorController
        var controller = root.AddComponent<SpectatorController>();

        // Create Camera as child
        GameObject cameraObj = new GameObject("SpectatorCamera");
        cameraObj.transform.SetParent(root.transform);
        cameraObj.transform.localPosition = Vector3.zero;
        cameraObj.transform.localRotation = Quaternion.identity;

        Camera cam = cameraObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;

        // Add AudioListener
        cameraObj.AddComponent<AudioListener>();

        // Create Canvas for UI
        GameObject canvasObj = new GameObject("SpectatorCanvas");
        canvasObj.transform.SetParent(root.transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Create SpectatorUI Panel
        GameObject uiPanel = new GameObject("SpectatorUI");
        uiPanel.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = uiPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var spectatorUI = uiPanel.AddComponent<SpectatorUI>();

        // Create Mode Text (top left)
        GameObject modeTextObj = CreateTextElement(
            "ModeText",
            uiPanel.transform,
            new Vector2(0, 1),
            new Vector2(0, 1), // Top-left anchor
            new Vector2(30, -30),
            new Vector2(500, 50),
            "SPECTATOR - Overview Mode",
            32,
            TextAlignmentOptions.Left,
            new Color(0.3f, 0.8f, 1f)
        );

        // Create Controls Text (bottom left)
        GameObject controlsTextObj = CreateTextElement(
            "ControlsText",
            uiPanel.transform,
            new Vector2(0, 0),
            new Vector2(0, 0), // Bottom-left anchor
            new Vector2(30, 30),
            new Vector2(400, 150),
            "<b>Controls:</b>\nWASD - Move\nRight Click + Mouse - Look\nShift - Speed Up\n<color=#FFD700>Tab - Switch Mode</color>",
            18,
            TextAlignmentOptions.Left,
            Color.white
        );

        // Create Player Follow Panel (center-bottom, hidden by default)
        GameObject followPanel = new GameObject("PlayerFollowPanel");
        followPanel.transform.SetParent(uiPanel.transform, false);

        RectTransform followRect = followPanel.AddComponent<RectTransform>();
        followRect.anchorMin = new Vector2(0.5f, 0);
        followRect.anchorMax = new Vector2(0.5f, 0);
        followRect.pivot = new Vector2(0.5f, 0);
        followRect.anchoredPosition = new Vector2(0, 50);
        followRect.sizeDelta = new Vector2(400, 60);

        // Add background to follow panel
        Image followBg = followPanel.AddComponent<Image>();
        followBg.color = new Color(0, 0, 0, 0.6f);

        // Player name text inside follow panel
        GameObject playerNameObj = CreateTextElement(
            "PlayerNameText",
            followPanel.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(380, 50),
            "Following: Player Name",
            24,
            TextAlignmentOptions.Center,
            new Color(1f, 0.9f, 0.5f)
        );

        followPanel.SetActive(false); // Hidden by default

        // Create spectator indicator (top right)
        GameObject indicatorObj = CreateTextElement(
            "SpectatorIndicator",
            uiPanel.transform,
            new Vector2(1, 1),
            new Vector2(1, 1), // Top-right anchor
            new Vector2(-30, -30),
            new Vector2(200, 40),
            "[  ] SPECTATING",  // Use brackets instead of emoji for font compatibility
            20,
            TextAlignmentOptions.Right,
            new Color(1f, 0.5f, 0.5f, 0.8f)
        );

        // Wire up SpectatorUI references using SerializedObject
        SerializedObject so = new SerializedObject(spectatorUI);
        so.FindProperty("_modeText").objectReferenceValue = modeTextObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_controlsText").objectReferenceValue = controlsTextObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_playerNameText").objectReferenceValue = playerNameObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_playerFollowPanel").objectReferenceValue = followPanel;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Ensure directory exists
        string prefabPath = "Assets/Prefabs/Spectator";
        if (!AssetDatabase.IsValidFolder(prefabPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            AssetDatabase.CreateFolder("Assets/Prefabs", "Spectator");
        }

        // Save as prefab
        string fullPath = prefabPath + "/SpectatorCameraRig.prefab";

        // Check if prefab already exists
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
        if (existingPrefab != null)
        {
            if (
                !EditorUtility.DisplayDialog(
                    "Overwrite Prefab?",
                    "SpectatorCameraRig prefab already exists. Overwrite?",
                    "Yes",
                    "No"
                )
            )
            {
                DestroyImmediate(root);
                return;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, fullPath);
        DestroyImmediate(root);

        Debug.Log($"<color=green>[SpectatorCameraRigCreator] Created prefab at: {fullPath}</color>");
        EditorUtility.DisplayDialog(
            "Success!",
            $"SpectatorCameraRig prefab created at:\n{fullPath}\n\nAssign it to SpectatorSpawner in your game scenes!",
            "OK"
        );

        // Ping the created asset
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(fullPath));
    }

    private static GameObject CreateTextElement(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size,
        string text,
        int fontSize,
        TextAlignmentOptions alignment,
        Color color
    )
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin; // Pivot matches anchor for easier positioning
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;

        // Add outline for better readability
        tmp.fontStyle = FontStyles.Normal;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 128);

        return obj;
    }
}
#endif
