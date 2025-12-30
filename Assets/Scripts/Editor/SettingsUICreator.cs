#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Editor utility to create the Settings UI prefab.
/// Run from menu: Tools > UI > Create Settings UI
/// </summary>
public class SettingsUICreator : EditorWindow
{
    private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.18f, 0.95f);
    private static readonly Color AccentColor = new Color(0f, 0.85f, 1f, 1f);
    private static readonly Color TextColor = Color.white;
    private static readonly Color SecondaryTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.2f, 0.25f, 1f);
    private static readonly Color SliderBgColor = new Color(0.15f, 0.15f, 0.2f, 1f);

    [MenuItem("Tools/UI/Create Settings UI")]
    public static void CreateSettingsUI()
    {
        // Create root object
        GameObject root = new GameObject("SettingsUI");

        // Add SettingsUI component
        var settingsUI = root.AddComponent<SettingsUI>();

        // Create Settings Button (for main menu)
        GameObject settingsBtn = CreateButton(root.transform, "SettingsButton", "⚙ SETTINGS", 
            new Vector2(180, 50), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30));

        // Create Settings Panel
        GameObject panel = CreateSettingsPanel(root.transform);

        // Wire up references using SerializedObject
        WireUpReferences(settingsUI, settingsBtn, panel);

        // Ensure directory exists
        string prefabPath = "Assets/Prefabs/UI";
        if (!AssetDatabase.IsValidFolder(prefabPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        // Save as prefab
        string fullPath = prefabPath + "/SettingsUI.prefab";

        // Check if prefab already exists
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
        if (existingPrefab != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Overwrite Prefab?",
                    "SettingsUI prefab already exists. Overwrite?",
                    "Yes",
                    "No"))
            {
                DestroyImmediate(root);
                return;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, fullPath);
        DestroyImmediate(root);

        Debug.Log($"<color=green>[SettingsUICreator] Created prefab at: {fullPath}</color>");
        EditorUtility.DisplayDialog(
            "Success!",
            $"SettingsUI prefab created at:\n{fullPath}\n\nDrag it into your Menu scene Canvas!",
            "OK"
        );

        // Ping the created asset
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(fullPath));
    }

    private static GameObject CreateSettingsPanel(Transform parent)
    {
        // Main Panel
        GameObject panel = new GameObject("SettingsPanel");
        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(550, 620);
        panelRect.anchoredPosition = Vector2.zero;

        // Background
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = BackgroundColor;

        // Border glow effect
        GameObject border = new GameObject("Border");
        border.transform.SetParent(panel.transform, false);
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(4, 4);
        borderRect.anchoredPosition = Vector2.zero;
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = AccentColor;
        borderImg.raycastTarget = false;

        // Inner background
        GameObject inner = new GameObject("InnerBackground");
        inner.transform.SetParent(panel.transform, false);
        RectTransform innerRect = inner.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.sizeDelta = new Vector2(-4, -4);
        innerRect.anchoredPosition = Vector2.zero;
        Image innerBg = inner.AddComponent<Image>();
        innerBg.color = BackgroundColor;
        innerBg.raycastTarget = false;

        // Header
        CreateHeader(panel.transform);

        // Content Area
        CreateContent(panel.transform);

        // Footer
        CreateFooter(panel.transform);

        panel.SetActive(false); // Hidden by default
        return panel;
    }

    private static void CreateHeader(Transform parent)
    {
        GameObject header = new GameObject("Header");
        header.transform.SetParent(parent, false);

        RectTransform headerRect = header.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 60);
        headerRect.anchoredPosition = Vector2.zero;

        // Title
        GameObject title = CreateText(header.transform, "TitleText", "SETTINGS", 28, FontStyles.Bold, TextColor);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.sizeDelta = Vector2.zero;
        title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        // Close Button
        GameObject closeBtn = CreateButton(header.transform, "CloseButton", "✕", 
            new Vector2(40, 40), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10));
        closeBtn.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
    }

    private static void CreateContent(Transform parent)
    {
        GameObject content = new GameObject("Content");
        content.transform.SetParent(parent, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.offsetMin = new Vector2(30, 70);
        contentRect.offsetMax = new Vector2(-30, -70);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.padding = new RectOffset(0, 0, 10, 10);

        // Audio Section Header
        CreateSectionTitle(content.transform, "AUDIO");

        // Master Volume
        CreateSliderRow(content.transform, "MasterVolumeRow", "Master Volume", "MasterSlider", "MasterValueText");
        
        // Music Volume
        CreateSliderRow(content.transform, "MusicVolumeRow", "Music Volume", "MusicSlider", "MusicValueText");
        
        // SFX Volume
        CreateSliderRow(content.transform, "SFXVolumeRow", "Sound Effects", "SFXSlider", "SFXValueText");

        // Spacer
        CreateSpacer(content.transform, 10);

        // Display Section Header
        CreateSectionTitle(content.transform, "DISPLAY");

        // Fullscreen Toggle
        CreateToggleRow(content.transform, "FullscreenRow", "Fullscreen", "FullscreenToggle");

        // VSync Toggle
        CreateToggleRow(content.transform, "VSyncRow", "VSync", "VSyncToggle");

        // Graphics Quality Dropdown
        CreateDropdownRow(content.transform, "QualityRow", "Graphics Quality", "QualityDropdown");
    }

    private static void CreateFooter(Transform parent)
    {
        GameObject footer = new GameObject("Footer");
        footer.transform.SetParent(parent, false);

        RectTransform footerRect = footer.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0, 0);
        footerRect.anchorMax = new Vector2(1, 0);
        footerRect.pivot = new Vector2(0.5f, 0);
        footerRect.sizeDelta = new Vector2(0, 55);
        footerRect.anchoredPosition = new Vector2(0, 10);

        HorizontalLayoutGroup hlg = footer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Reset Defaults Button
        GameObject resetBtn = CreateLayoutButton(footer.transform, "ResetDefaultsButton", "Reset Defaults", new Vector2(160, 45));
        resetBtn.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.4f, 1f);

        // Apply Button
        GameObject applyBtn = CreateLayoutButton(footer.transform, "ApplyButton", "Apply & Close", new Vector2(160, 45));
        applyBtn.GetComponent<Image>().color = AccentColor;
        applyBtn.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0.1f, 0.1f, 0.15f, 1f);
    }

    private static void CreateSectionTitle(Transform parent, string text)
    {
        GameObject section = new GameObject($"{text}Title");
        section.transform.SetParent(parent, false);

        RectTransform sectionRect = section.AddComponent<RectTransform>();
        sectionRect.sizeDelta = new Vector2(0, 30);

        LayoutElement le = section.AddComponent<LayoutElement>();
        le.preferredHeight = 30;
        le.flexibleWidth = 1;

        GameObject title = CreateText(section.transform, "Text", text, 15, FontStyles.Bold, AccentColor);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.sizeDelta = Vector2.zero;
        title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.BottomLeft;
    }

    private static void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(parent, false);
        spacer.AddComponent<RectTransform>().sizeDelta = new Vector2(0, height);
        LayoutElement le = spacer.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }

    private static void CreateSliderRow(Transform parent, string rowName, string labelText, string sliderName, string valueTextName)
    {
        GameObject row = new GameObject(rowName);
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 35);

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 35;
        le.flexibleWidth = 1;

        // Label (left 35%)
        GameObject label = CreateText(row.transform, "Label", labelText, 15, FontStyles.Normal, TextColor);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0.35f, 1);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

        // Slider (middle 50%)
        GameObject slider = CreateSlider(row.transform, sliderName);
        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.37f, 0.25f);
        sliderRect.anchorMax = new Vector2(0.87f, 0.75f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        // Value Text (right 13%)
        GameObject valueText = CreateText(row.transform, valueTextName, "100%", 14, FontStyles.Normal, SecondaryTextColor);
        RectTransform valueRect = valueText.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0.88f, 0);
        valueRect.anchorMax = new Vector2(1, 1);
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;
        valueText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;
    }

    private static void CreateToggleRow(Transform parent, string rowName, string labelText, string toggleName)
    {
        GameObject row = new GameObject(rowName);
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 35);

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 35;
        le.flexibleWidth = 1;

        // Label
        GameObject label = CreateText(row.transform, "Label", labelText, 15, FontStyles.Normal, TextColor);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0.7f, 1);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

        // Toggle (right side)
        GameObject toggle = CreateToggle(row.transform, toggleName);
        RectTransform toggleRect = toggle.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.88f, 0.15f);
        toggleRect.anchorMax = new Vector2(1, 0.85f);
        toggleRect.offsetMin = Vector2.zero;
        toggleRect.offsetMax = Vector2.zero;
    }

    private static void CreateDropdownRow(Transform parent, string rowName, string labelText, string dropdownName)
    {
        GameObject row = new GameObject(rowName);
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 40);

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 40;
        le.flexibleWidth = 1;

        // Label
        GameObject label = CreateText(row.transform, "Label", labelText, 15, FontStyles.Normal, TextColor);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0.35f, 1);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

        // Dropdown
        GameObject dropdown = CreateDropdown(row.transform, dropdownName);
        RectTransform dropdownRect = dropdown.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0.37f, 0.1f);
        dropdownRect.anchorMax = new Vector2(1, 0.9f);
        dropdownRect.offsetMin = Vector2.zero;
        dropdownRect.offsetMax = Vector2.zero;
    }

    #region UI Element Creators

    private static GameObject CreateText(Transform parent, string name, string text, int fontSize, FontStyles style, Color color)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);
        textGO.AddComponent<RectTransform>();

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        return textGO;
    }

    private static GameObject CreateButton(Transform parent, string name, string text, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 position)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = ButtonColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(AccentColor.r * 0.8f, AccentColor.g * 0.8f, AccentColor.b * 0.8f, 1f);
        colors.pressedColor = AccentColor;
        btn.colors = colors;

        // Button text
        GameObject textGO = CreateText(btnGO.transform, "Text", text, 16, FontStyles.Bold, TextColor);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        return btnGO;
    }

    private static GameObject CreateLayoutButton(Transform parent, string name, string text, Vector2 size)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.sizeDelta = size;

        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.preferredWidth = size.x;
        le.preferredHeight = size.y;

        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = ButtonColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(AccentColor.r * 0.7f, AccentColor.g * 0.7f, AccentColor.b * 0.7f, 1f);
        colors.pressedColor = AccentColor;
        btn.colors = colors;

        // Button text
        GameObject textGO = CreateText(btnGO.transform, "Text", text, 15, FontStyles.Bold, TextColor);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        return btnGO;
    }

    private static GameObject CreateSlider(Transform parent, string name)
    {
        GameObject sliderGO = new GameObject(name);
        sliderGO.transform.SetParent(parent, false);
        sliderGO.AddComponent<RectTransform>();

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.25f);
        bgRect.anchorMax = new Vector2(1, 0.75f);
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = SliderBgColor;

        // Fill Area
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5, 0);
        fillAreaRect.offsetMax = new Vector2(-5, 0);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = AccentColor;

        // Handle Slide Area
        GameObject handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10, 0);
        handleAreaRect.offsetMax = new Vector2(-10, 0);

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        RectTransform handleRect = handleGO.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18, 0);
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;

        // Slider component
        Slider slider = sliderGO.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 1;

        return sliderGO;
    }

    private static GameObject CreateToggle(Transform parent, string name)
    {
        GameObject toggleGO = new GameObject(name);
        toggleGO.transform.SetParent(parent, false);
        toggleGO.AddComponent<RectTransform>();

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(toggleGO.transform, false);
        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = SliderBgColor;

        // Checkmark
        GameObject checkGO = new GameObject("Checkmark");
        checkGO.transform.SetParent(bgGO.transform, false);
        RectTransform checkRect = checkGO.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.15f, 0.15f);
        checkRect.anchorMax = new Vector2(0.85f, 0.85f);
        checkRect.sizeDelta = Vector2.zero;
        Image checkImg = checkGO.AddComponent<Image>();
        checkImg.color = AccentColor;

        // Toggle component
        Toggle toggle = toggleGO.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = true;

        return toggleGO;
    }

    private static GameObject CreateDropdown(Transform parent, string name)
    {
        GameObject dropdownGO = new GameObject(name);
        dropdownGO.transform.SetParent(parent, false);
        dropdownGO.AddComponent<RectTransform>();

        Image dropdownImg = dropdownGO.AddComponent<Image>();
        dropdownImg.color = SliderBgColor;

        // Label
        GameObject labelGO = CreateText(dropdownGO.transform, "Label", "High", 14, FontStyles.Normal, TextColor);
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 0);
        labelRect.offsetMax = new Vector2(-30, 0);
        labelGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

        // Arrow
        GameObject arrowGO = CreateText(dropdownGO.transform, "Arrow", "▼", 12, FontStyles.Normal, SecondaryTextColor);
        RectTransform arrowRect = arrowGO.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1, 0);
        arrowRect.anchorMax = new Vector2(1, 1);
        arrowRect.sizeDelta = new Vector2(25, 0);
        arrowRect.anchoredPosition = new Vector2(-12.5f, 0);
        arrowGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        // Template
        GameObject templateGO = new GameObject("Template");
        templateGO.transform.SetParent(dropdownGO.transform, false);
        RectTransform templateRect = templateGO.AddComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0, 0);
        templateRect.anchorMax = new Vector2(1, 0);
        templateRect.pivot = new Vector2(0.5f, 1);
        templateRect.sizeDelta = new Vector2(0, 150);
        Image templateImg = templateGO.AddComponent<Image>();
        templateImg.color = new Color(0.12f, 0.12f, 0.18f, 1f);

        ScrollRect scrollRect = templateGO.AddComponent<ScrollRect>();
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(templateGO.transform, false);
        RectTransform viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.pivot = new Vector2(0, 1);
        Mask mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Image viewportImg = viewportGO.AddComponent<Image>();
        viewportImg.color = Color.white;

        scrollRect.viewport = viewportRect;

        // Content
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 28);

        scrollRect.content = contentRect;

        // Item
        GameObject itemGO = new GameObject("Item");
        itemGO.transform.SetParent(contentGO.transform, false);
        RectTransform itemRect = itemGO.AddComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 0.5f);
        itemRect.anchorMax = new Vector2(1, 0.5f);
        itemRect.sizeDelta = new Vector2(0, 28);
        Toggle itemToggle = itemGO.AddComponent<Toggle>();

        // Item Background
        GameObject itemBgGO = new GameObject("Item Background");
        itemBgGO.transform.SetParent(itemGO.transform, false);
        RectTransform itemBgRect = itemBgGO.AddComponent<RectTransform>();
        itemBgRect.anchorMin = Vector2.zero;
        itemBgRect.anchorMax = Vector2.one;
        itemBgRect.sizeDelta = Vector2.zero;
        Image itemBgImg = itemBgGO.AddComponent<Image>();
        itemBgImg.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.25f);

        // Item Checkmark
        GameObject itemCheckGO = CreateText(itemGO.transform, "Item Checkmark", "✓", 14, FontStyles.Bold, AccentColor);
        RectTransform itemCheckRect = itemCheckGO.GetComponent<RectTransform>();
        itemCheckRect.anchorMin = new Vector2(0, 0);
        itemCheckRect.anchorMax = new Vector2(0, 1);
        itemCheckRect.sizeDelta = new Vector2(25, 0);
        itemCheckRect.anchoredPosition = new Vector2(12.5f, 0);
        itemCheckGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        // Item Label
        GameObject itemLabelGO = CreateText(itemGO.transform, "Item Label", "Option", 14, FontStyles.Normal, TextColor);
        RectTransform itemLabelRect = itemLabelGO.GetComponent<RectTransform>();
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(25, 0);
        itemLabelRect.offsetMax = Vector2.zero;
        itemLabelGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic = itemCheckGO.GetComponent<TextMeshProUGUI>();
        itemToggle.isOn = true;

        templateGO.SetActive(false);

        // TMP Dropdown component
        TMP_Dropdown dropdown = dropdownGO.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = dropdownImg;
        dropdown.template = templateRect;
        dropdown.captionText = labelGO.GetComponent<TextMeshProUGUI>();
        dropdown.itemText = itemLabelGO.GetComponent<TextMeshProUGUI>();

        // Add default options
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string> { "Low", "Medium", "High", "Ultra" });
        dropdown.value = 2; // Default to High

        return dropdownGO;
    }

    #endregion

    private static void WireUpReferences(SettingsUI ui, GameObject settingsBtn, GameObject panel)
    {
        SerializedObject so = new SerializedObject(ui);

        // Panel references
        so.FindProperty("settingsPanel").objectReferenceValue = panel;
        so.FindProperty("openSettingsButton").objectReferenceValue = settingsBtn.GetComponent<Button>();
        so.FindProperty("closeButton").objectReferenceValue = panel.transform.Find("Header/CloseButton").GetComponent<Button>();

        // Audio sliders
        so.FindProperty("masterVolumeSlider").objectReferenceValue = panel.transform.Find("Content/MasterVolumeRow/MasterSlider").GetComponent<Slider>();
        so.FindProperty("masterVolumeText").objectReferenceValue = panel.transform.Find("Content/MasterVolumeRow/MasterValueText").GetComponent<TextMeshProUGUI>();
        so.FindProperty("musicVolumeSlider").objectReferenceValue = panel.transform.Find("Content/MusicVolumeRow/MusicSlider").GetComponent<Slider>();
        so.FindProperty("musicVolumeText").objectReferenceValue = panel.transform.Find("Content/MusicVolumeRow/MusicValueText").GetComponent<TextMeshProUGUI>();
        so.FindProperty("sfxVolumeSlider").objectReferenceValue = panel.transform.Find("Content/SFXVolumeRow/SFXSlider").GetComponent<Slider>();
        so.FindProperty("sfxVolumeText").objectReferenceValue = panel.transform.Find("Content/SFXVolumeRow/SFXValueText").GetComponent<TextMeshProUGUI>();

        // Display toggles
        so.FindProperty("fullscreenToggle").objectReferenceValue = panel.transform.Find("Content/FullscreenRow/FullscreenToggle").GetComponent<Toggle>();
        so.FindProperty("vsyncToggle").objectReferenceValue = panel.transform.Find("Content/VSyncRow/VSyncToggle").GetComponent<Toggle>();
        so.FindProperty("qualityDropdown").objectReferenceValue = panel.transform.Find("Content/QualityRow/QualityDropdown").GetComponent<TMP_Dropdown>();

        // Footer buttons
        so.FindProperty("applyButton").objectReferenceValue = panel.transform.Find("Footer/ApplyButton").GetComponent<Button>();
        so.FindProperty("resetDefaultsButton").objectReferenceValue = panel.transform.Find("Footer/ResetDefaultsButton").GetComponent<Button>();

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
