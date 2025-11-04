using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class JoinGameManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject homeWindow;
    public GameObject joinGameWindow;
    public Button joinGameButton;
    public Button backButton;

    [Header("Lobby List References")]
    public Transform lobbyListContent;
    public GameObject lobbyItemPrefab;

    [Header("Table Header Reference")]
    public GameObject tableHeader;
    [Header("Display Settings")]
    public float rowHeight = 60f;
    public float rowSpacing = 5f;
    public Color rowColor1 = new Color(0.95f, 0.95f, 0.95f, 1f);
    public Color rowColor2 = new Color(0.90f, 0.90f, 0.90f, 1f);

    void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (joinGameWindow != null)
            joinGameWindow.SetActive(false);

        if (joinGameButton != null)
            joinGameButton.onClick.AddListener(OnJoinGameClicked);

        if (backButton != null)
            backButton.onClick.AddListener(ShowHomeWindow);
    }

    public void OnJoinGameClicked()
    {
        if (homeWindow != null)
            homeWindow.SetActive(false);

        if (joinGameWindow != null)
        {
            joinGameWindow.SetActive(true);
            ClearContent();
            SetupContentLayout();
            CreateLobbyRows();
        }
    }

    private void SetupContentLayout()
    {
        if (lobbyListContent == null) return;

        VerticalLayoutGroup layout = lobbyListContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = lobbyListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = rowSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        ContentSizeFitter sizeFitter = lobbyListContent.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = lobbyListContent.gameObject.AddComponent<ContentSizeFitter>();
        }
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;


    }

    private void ClearContent()
    {
        if (lobbyListContent == null) return;

        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        Debug.Log("Cleared all content children");
    }

    private void CreateLobbyRows()
    {
        if (lobbyListContent == null || lobbyItemPrefab == null) return;

        List<LobbyData> sampleLobbies = new List<LobbyData>
        {
            new LobbyData {
                lobbyName = "Alpha Squad",
                lobbyLeader = "ProPlayer1",
                playerCapacity = "3/8",
                status = "Waiting",
                preset = "Competitive",
                region = "Asia"
            },
            new LobbyData {
                lobbyName = "Casual Fun House",
                lobbyLeader = "FunGamer",
                playerCapacity = "6/8",
                status = "In Game",
                preset = "Casual",
                region = "Europe"
            },
            new LobbyData {
                lobbyName = "Pro Tournament",
                lobbyLeader = "TournamentMaster",
                playerCapacity = "8/8",
                status = "Full",
                preset = "Tournament",
                region = "USA"
            }
        };

        for (int i = 0; i < sampleLobbies.Count; i++)
        {
            CreateLobbyRow(sampleLobbies[i], i);
        }

        Debug.Log($"Created {sampleLobbies.Count} lobby rows");
    }

    private void CreateLobbyRow(LobbyData lobbyData, int rowIndex)
    {
        try
        {
            GameObject lobbyRow = Instantiate(lobbyItemPrefab, lobbyListContent);
            lobbyRow.name = "LobbyRow_" + rowIndex;

            SetupRowLayout(lobbyRow);
            SetupRowBackground(lobbyRow, rowIndex);
            AlignColumnsWithHeader(lobbyRow);
            SetupRowText(lobbyRow, lobbyData);
            SetupRowClickEvent(lobbyRow, lobbyData);

        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to create lobby row: " + e.Message);
        }
    }

    private void SetupRowLayout(GameObject lobbyRow)
    {
        RectTransform rowRect = lobbyRow.GetComponent<RectTransform>();
        if (rowRect == null) return;

        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot = new Vector2(0.5f, 1);
        rowRect.sizeDelta = new Vector2(0, rowHeight);

        LayoutElement layoutElement = lobbyRow.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = lobbyRow.AddComponent<LayoutElement>();
        }
        layoutElement.preferredHeight = rowHeight;
        layoutElement.minHeight = rowHeight;
        layoutElement.flexibleWidth = 1;
    }

    private void SetupRowBackground(GameObject lobbyRow, int rowIndex)
    {
        Image bgImage = lobbyRow.GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = lobbyRow.AddComponent<Image>();
        }

        bgImage.color = (rowIndex % 2 == 0) ? rowColor1 : rowColor2;

        Outline outline = lobbyRow.GetComponent<Outline>();
        if (outline == null)
        {
            outline = lobbyRow.AddComponent<Outline>();
        }
        outline.effectColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        outline.effectDistance = new Vector2(1, -1);
    }

    private void AlignColumnsWithHeader(GameObject lobbyRow)
    {
        if (tableHeader == null) return;

        TextMeshProUGUI[] headerTexts = tableHeader.GetComponentsInChildren<TextMeshProUGUI>();
        TextMeshProUGUI[] rowTexts = lobbyRow.GetComponentsInChildren<TextMeshProUGUI>();

        for (int i = 0; i < Mathf.Min(headerTexts.Length, rowTexts.Length); i++)
        {
            RectTransform headerRect = headerTexts[i].GetComponent<RectTransform>();
            RectTransform rowRect = rowTexts[i].GetComponent<RectTransform>();

            if (headerRect != null && rowRect != null)
            {
                rowRect.anchorMin = headerRect.anchorMin;
                rowRect.anchorMax = headerRect.anchorMax;
                rowRect.pivot = headerRect.pivot;
                rowRect.anchoredPosition = headerRect.anchoredPosition;
                rowRect.sizeDelta = headerRect.sizeDelta;

                rowTexts[i].alignment = headerTexts[i].alignment;

                Debug.Log($"Aligned column {i}: {headerTexts[i].name} -> {rowTexts[i].name}");
            }
        }
    }

    private void SetupRowText(GameObject lobbyRow, LobbyData lobbyData)
    {
        TextMeshProUGUI[] texts = lobbyRow.GetComponentsInChildren<TextMeshProUGUI>();

        foreach (TextMeshProUGUI text in texts)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = Color.black;
            text.fontSize = 16;

            if (text.name.ToLower().Contains("lobbyname") || text.name.Contains("(1)"))
            {
                text.text = lobbyData.lobbyName;
            }
            else if (text.name.ToLower().Contains("lobbyleader") || text.name.Contains("(2)"))
            {
                text.text = lobbyData.lobbyLeader;
            }
            else if (text.name.ToLower().Contains("playercapacity") || text.name.Contains("(3)"))
            {
                text.text = lobbyData.playerCapacity;
            }
            else if (text.name.ToLower().Contains("status") || text.name.Contains("(4)"))
            {
                text.text = lobbyData.status;
            }
            else if (text.name.ToLower().Contains("preset") || text.name.Contains("(5)"))
            {
                text.text = lobbyData.preset;
            }
            else if (text.name.ToLower().Contains("region") || text.name.Contains("(6)"))
            {
                text.text = lobbyData.region;
            }
        }
    }

    private void SetupRowClickEvent(GameObject lobbyRow, LobbyData lobbyData)
    {
        Button clickButton = lobbyRow.GetComponent<Button>();
        if (clickButton == null)
        {
            clickButton = lobbyRow.AddComponent<Button>();
        }

        ColorBlock colors = clickButton.colors;
        colors.normalColor = new Color(1, 1, 1, 0f);
        colors.highlightedColor = new Color(0.9f, 0.95f, 1f, 0.2f);
        colors.pressedColor = new Color(0.8f, 0.9f, 1f, 0.3f);
        clickButton.colors = colors;

        clickButton.onClick.RemoveAllListeners();
        clickButton.onClick.AddListener(() => JoinRoomInstantly(lobbyData));
    }

    public void JoinRoomInstantly(LobbyData lobbyData)
    {
        Debug.Log("Joining room: " + lobbyData.lobbyName);
        if (joinGameWindow != null) joinGameWindow.SetActive(false);
    }

    public void ShowHomeWindow()
    {
        ClearContent();
        if (joinGameWindow != null) joinGameWindow.SetActive(false);
        if (homeWindow != null) homeWindow.SetActive(true);
    }

    void OnDestroy()
    {
        ClearContent();
    }
}
