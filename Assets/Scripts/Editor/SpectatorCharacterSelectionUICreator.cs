#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor utility to create the SpectatorCharacterSelectionUI prefab.
/// Run from menu: Tools > Spectator > Create Character Selection Spectator UI
/// </summary>
public class SpectatorCharacterSelectionUICreator : EditorWindow
{
    [MenuItem("Tools/Spectator/Create Character Selection Spectator UI")]
    public static void CreateSpectatorCharacterSelectionUI()
    {
        // Create root object
        GameObject root = new GameObject("SpectatorCharacterSelectionUI");

        // Add the component
        var spectatorUI = root.AddComponent<SpectatorCharacterSelectionUI>();

        // Create Canvas
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // Above character selection UI

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // Create spectator panel (full screen overlay)
        GameObject panel = new GameObject("SpectatorPanel");
        panel.transform.SetParent(root.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Semi-transparent background
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.7f);

        // Create content container (centered)
        GameObject content = new GameObject("Content");
        content.transform.SetParent(panel.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(800, 400);

        // Background for content box
        Image contentBg = content.AddComponent<Image>();
        contentBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // Add rounded corners effect (outline)
        Outline outline = content.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.7f, 1f, 0.8f);
        outline.effectDistance = new Vector2(3, -3);

        // Create spectator icon/emoji
        GameObject iconObj = new GameObject("SpectatorIcon");
        iconObj.transform.SetParent(content.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0, 80);
        iconRect.sizeDelta = new Vector2(100, 100);

        TextMeshProUGUI iconText = iconObj.AddComponent<TextMeshProUGUI>();
        iconText.text = "👁";
        iconText.fontSize = 72;
        iconText.alignment = TextAlignmentOptions.Center;
        iconText.color = new Color(0.3f, 0.8f, 1f);

        // Create title text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(content.transform, false);

        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0, 0);
        titleRect.sizeDelta = new Vector2(700, 60);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "SPECTATOR MODE";
        titleText.fontSize = 48;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.3f, 0.8f, 1f);

        // Create status text
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(content.transform, false);

        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(0, -60);
        statusRect.sizeDelta = new Vector2(700, 80);

        TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.text = "You are spectating.\nWaiting for players to start the game...";
        statusText.fontSize = 28;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = Color.white;
        statusText.enableWordWrapping = true;

        // Create hint text
        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(content.transform, false);

        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0, -130);
        hintRect.sizeDelta = new Vector2(700, 40);

        TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
        hintText.text = "The match will begin shortly...";
        hintText.fontSize = 20;
        hintText.fontStyle = FontStyles.Italic;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(0.7f, 0.7f, 0.7f);

        // Wire up references using SerializedObject
        SerializedObject so = new SerializedObject(spectatorUI);
        so.FindProperty("_spectatorPanel").objectReferenceValue = panel;
        so.FindProperty("_statusText").objectReferenceValue = statusText;
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
        string fullPath = prefabPath + "/SpectatorCharacterSelectionUI.prefab";

        // Check if prefab already exists
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
        if (existingPrefab != null)
        {
            if (
                !EditorUtility.DisplayDialog(
                    "Overwrite Prefab?",
                    "SpectatorCharacterSelectionUI prefab already exists. Overwrite?",
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

        Debug.Log($"<color=green>[SpectatorCharacterSelectionUICreator] Created prefab at: {fullPath}</color>");
        EditorUtility.DisplayDialog(
            "Success!",
            $"SpectatorCharacterSelectionUI prefab created at:\n{fullPath}\n\nDrag it into your CharacterSelection scene!",
            "OK"
        );

        // Ping the created asset
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(fullPath));
    }
}
#endif
