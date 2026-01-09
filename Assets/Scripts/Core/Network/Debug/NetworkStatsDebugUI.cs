using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Network Statistics Debug UI - Hiển thị thông tin network realtime.
/// Sử dụng cho demo/thesis defense để show latency, packets, bandwidth.
/// 
/// Cách sử dụng:
/// 1. Gắn script này vào một GameObject trong scene Game
/// 2. Nhấn F3 để toggle hiển thị
/// 3. Có thể điều chỉnh vị trí/kích thước qua Inspector
/// </summary>
public class NetworkStatsDebugUI : MonoBehaviour
{
    [Header("Display Settings")]
    [SerializeField] private bool showByDefault = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
    [SerializeField] private Rect windowRect = new Rect(10, 10, 350, 300);
    
    [Header("Colors")]
    [SerializeField] private Color goodLatencyColor = Color.green;
    [SerializeField] private Color mediumLatencyColor = Color.yellow;
    [SerializeField] private Color badLatencyColor = Color.red;
    
    [Header("Thresholds (ms)")]
    [SerializeField] private float goodLatencyThreshold = 50f;
    [SerializeField] private float mediumLatencyThreshold = 100f;
    
    // Stats tracking
    private bool _isVisible;
    private float _currentRTT;
    private float _averageRTT;
    private float _minRTT = float.MaxValue;
    private float _maxRTT = 0f;
    private int _rttSampleCount;
    private float _rttSum;
    
    // Ping measurement
    private float _lastPingTime;
    private float _pingInterval = 0.5f;
    private int _pingsSent;
    private int _pingsReceived;
    
    // Packet tracking
    private ulong _lastBytesSent;
    private ulong _lastBytesReceived;
    private float _lastBandwidthCheck;
    private float _outgoingBandwidth;
    private float _incomingBandwidth;
    
    // GUI styles
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _valueStyle;
    private bool _stylesInitialized;
    
    private void Start()
    {
        _isVisible = showByDefault;
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            _isVisible = !_isVisible;
        }
        
        if (!_isVisible) return;
        
        UpdateNetworkStats();
    }
    
    private void UpdateNetworkStats()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            return;
            
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null) return;
        
        // Get RTT from transport
        ulong serverClientId = NetworkManager.Singleton.IsServer ? 
            NetworkManager.Singleton.LocalClientId : 
            NetworkManager.ServerClientId;
            
        _currentRTT = transport.GetCurrentRtt(serverClientId);
        
        // Track RTT statistics
        if (_currentRTT > 0)
        {
            _rttSampleCount++;
            _rttSum += _currentRTT;
            _averageRTT = _rttSum / _rttSampleCount;
            
            if (_currentRTT < _minRTT) _minRTT = _currentRTT;
            if (_currentRTT > _maxRTT) _maxRTT = _currentRTT;
        }
        
        // Calculate bandwidth (every second)
        float timeSinceLastCheck = Time.time - _lastBandwidthCheck;
        if (timeSinceLastCheck >= 1f)
        {
            // Note: Unity Transport doesn't expose byte counters directly
            // This is a placeholder - in real implementation you'd track RPC sizes
            _lastBandwidthCheck = Time.time;
        }
    }
    
    private void InitStyles()
    {
        if (_stylesInitialized) return;
        
        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTexture(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.9f)) }
        };
        
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };
        
        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.cyan },
            alignment = TextAnchor.MiddleCenter
        };
        
        _valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        
        _stylesInitialized = true;
    }
    
    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
            
        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    
    private void OnGUI()
    {
        if (!_isVisible) return;
        
        InitStyles();
        
        windowRect = GUI.Window(12345, windowRect, DrawWindow, "", _boxStyle);
    }
    
    private void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        
        // Header
        GUILayout.Space(5);
        GUILayout.Label("📊 NETWORK STATS (F3 to toggle)", _headerStyle);
        GUILayout.Space(10);
        
        // Connection Status
        DrawSection("CONNECTION STATUS");
        
        bool isConnected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        
        string status = "Disconnected";
        Color statusColor = Color.red;
        
        if (isHost)
        {
            status = "HOST (Server + Client)";
            statusColor = Color.cyan;
        }
        else if (isServer)
        {
            status = "SERVER";
            statusColor = Color.green;
        }
        else if (isConnected)
        {
            status = "CLIENT (Connected)";
            statusColor = Color.green;
        }
        
        DrawStat("Status", status, statusColor);
        
        if (NetworkManager.Singleton != null)
        {
            DrawStat("Client ID", NetworkManager.Singleton.LocalClientId.ToString(), Color.white);
            DrawStat("Connected Clients", NetworkManager.Singleton.ConnectedClientsIds.Count.ToString(), Color.white);
        }
        
        GUILayout.Space(10);
        
        // Latency Stats
        DrawSection("LATENCY (Round-Trip Time)");
        
        Color rttColor = GetLatencyColor(_currentRTT);
        DrawStat("Current RTT", $"{_currentRTT:F1} ms", rttColor);
        DrawStat("Average RTT", $"{_averageRTT:F1} ms", Color.white);
        DrawStat("Min RTT", _minRTT < float.MaxValue ? $"{_minRTT:F1} ms" : "N/A", Color.green);
        DrawStat("Max RTT", _maxRTT > 0 ? $"{_maxRTT:F1} ms" : "N/A", Color.red);
        DrawStat("Jitter (Max-Min)", _minRTT < float.MaxValue ? $"{(_maxRTT - _minRTT):F1} ms" : "N/A", Color.yellow);
        
        GUILayout.Space(10);
        
        // Network Quality Assessment
        DrawSection("QUALITY ASSESSMENT");
        
        string quality = GetQualityAssessment(_averageRTT);
        Color qualityColor = GetLatencyColor(_averageRTT);
        DrawStat("Connection Quality", quality, qualityColor);
        DrawStat("Samples Collected", _rttSampleCount.ToString(), Color.white);
        
        GUILayout.Space(10);
        
        // Instructions
        GUILayout.Label("─────────────────────────────", _labelStyle);
        GUILayout.Label("Press [R] to reset stats", _labelStyle);
        
        GUILayout.EndVertical();
        
        // Handle reset
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R)
        {
            ResetStats();
            Event.current.Use();
        }
        
        // Make window draggable
        GUI.DragWindow();
    }
    
    private void DrawSection(string title)
    {
        GUILayout.Label("─────────────────────────────", _labelStyle);
        GUILayout.Label(title, _headerStyle);
    }
    
    private void DrawStat(string label, string value, Color valueColor)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + ":", _labelStyle, GUILayout.Width(140));
        
        _valueStyle.normal.textColor = valueColor;
        GUILayout.Label(value, _valueStyle);
        
        GUILayout.EndHorizontal();
    }
    
    private Color GetLatencyColor(float latency)
    {
        if (latency <= goodLatencyThreshold)
            return goodLatencyColor;
        if (latency <= mediumLatencyThreshold)
            return mediumLatencyColor;
        return badLatencyColor;
    }
    
    private string GetQualityAssessment(float avgRTT)
    {
        if (avgRTT <= 0) return "Measuring...";
        if (avgRTT <= 30) return "⭐ EXCELLENT";
        if (avgRTT <= 50) return "✓ GOOD";
        if (avgRTT <= 100) return "~ FAIR";
        if (avgRTT <= 150) return "! POOR";
        return "✗ BAD";
    }
    
    private void ResetStats()
    {
        _rttSampleCount = 0;
        _rttSum = 0;
        _averageRTT = 0;
        _minRTT = float.MaxValue;
        _maxRTT = 0;
        Debug.Log("[NetworkStatsDebugUI] Stats reset!");
    }
}
