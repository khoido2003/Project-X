using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Network Condition Simulator - Giả lập điều kiện mạng khác nhau để demo.
/// 
/// Cho phép simulate:
/// - Latency (độ trễ)
/// - Packet Loss (mất gói tin)
/// - Jitter (độ biến động)
/// 
/// Cách sử dụng:
/// 1. Gắn vào GameObject có NetworkManager
/// 2. Sử dụng Inspector hoặc gọi methods để thay đổi điều kiện
/// 3. Nhấn F4 để toggle simulation panel
/// 
/// LƯU Ý: Chỉ dùng cho DEMO/TESTING, không dùng trong production!
/// </summary>
public class NetworkConditionSimulator : MonoBehaviour
{
    [Header("Display Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F4;
    [SerializeField] private Rect windowRect = new Rect(370, 10, 300, 250);
    
    [Header("Simulation Settings")]
    [SerializeField] private bool enableSimulation = false;
    
    [Range(0, 500)]
    [SerializeField] private int simulatedLatencyMs = 0;
    
    [Range(0, 100)]
    [SerializeField] private int simulatedJitterMs = 0;
    
    [Range(0, 50)]
    [SerializeField] private int simulatedPacketLossPercent = 0;
    
    // Presets
    public enum NetworkPreset
    {
        Perfect,        // 0ms latency, 0% loss
        LAN,            // 5ms latency
        Broadband,      // 30ms latency
        WiFi,           // 50ms latency, 1% loss, 10ms jitter
        Mobile4G,       // 80ms latency, 2% loss, 20ms jitter
        Mobile3G,       // 150ms latency, 5% loss, 50ms jitter
        BadConnection,  // 300ms latency, 10% loss, 100ms jitter
        Terrible        // 500ms latency, 20% loss, 150ms jitter
    }
    
    private bool _isVisible = false;
    private GUIStyle _boxStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private bool _stylesInitialized;
    
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            _isVisible = !_isVisible;
        }
        
        if (enableSimulation)
        {
            ApplySimulationSettings();
        }
    }
    
    private void ApplySimulationSettings()
    {
        if (NetworkManager.Singleton == null) return;
        
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null) return;
        
        // Apply debug settings to Unity Transport
        // Note: These settings only work if SimulatorEnabled is true in transport
        var debugSimulator = transport.DebugSimulator;
        debugSimulator.PacketDelayMS = simulatedLatencyMs;
        debugSimulator.PacketJitterMS = simulatedJitterMs;
        debugSimulator.PacketDropRate = simulatedPacketLossPercent;
    }
    
    public void ApplyPreset(NetworkPreset preset)
    {
        switch (preset)
        {
            case NetworkPreset.Perfect:
                simulatedLatencyMs = 0;
                simulatedJitterMs = 0;
                simulatedPacketLossPercent = 0;
                break;
                
            case NetworkPreset.LAN:
                simulatedLatencyMs = 5;
                simulatedJitterMs = 2;
                simulatedPacketLossPercent = 0;
                break;
                
            case NetworkPreset.Broadband:
                simulatedLatencyMs = 30;
                simulatedJitterMs = 5;
                simulatedPacketLossPercent = 0;
                break;
                
            case NetworkPreset.WiFi:
                simulatedLatencyMs = 50;
                simulatedJitterMs = 10;
                simulatedPacketLossPercent = 1;
                break;
                
            case NetworkPreset.Mobile4G:
                simulatedLatencyMs = 80;
                simulatedJitterMs = 20;
                simulatedPacketLossPercent = 2;
                break;
                
            case NetworkPreset.Mobile3G:
                simulatedLatencyMs = 150;
                simulatedJitterMs = 50;
                simulatedPacketLossPercent = 5;
                break;
                
            case NetworkPreset.BadConnection:
                simulatedLatencyMs = 300;
                simulatedJitterMs = 100;
                simulatedPacketLossPercent = 10;
                break;
                
            case NetworkPreset.Terrible:
                simulatedLatencyMs = 500;
                simulatedJitterMs = 150;
                simulatedPacketLossPercent = 20;
                break;
        }
        
        Debug.Log($"[NetworkConditionSimulator] Applied preset: {preset}");
    }
    
    private void InitStyles()
    {
        if (_stylesInitialized) return;
        
        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTexture(2, 2, new Color(0.15f, 0.1f, 0.1f, 0.95f)) }
        };
        
        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow },
            alignment = TextAnchor.MiddleCenter
        };
        
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };
        
        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11
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
        
        windowRect = GUI.Window(12346, windowRect, DrawWindow, "", _boxStyle);
    }
    
    private void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        
        GUILayout.Space(5);
        GUILayout.Label("⚠️ NETWORK SIMULATOR (F4)", _headerStyle);
        GUILayout.Label("Demo/Testing Only!", _headerStyle);
        GUILayout.Space(10);
        
        // Enable toggle
        GUILayout.BeginHorizontal();
        GUILayout.Label("Simulation:", _labelStyle, GUILayout.Width(80));
        bool newEnabled = GUILayout.Toggle(enableSimulation, enableSimulation ? "ENABLED" : "DISABLED");
        if (newEnabled != enableSimulation)
        {
            enableSimulation = newEnabled;
            if (!enableSimulation)
            {
                ApplyPreset(NetworkPreset.Perfect);
            }
        }
        GUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // Sliders
        GUILayout.Label($"Latency: {simulatedLatencyMs}ms", _labelStyle);
        simulatedLatencyMs = (int)GUILayout.HorizontalSlider(simulatedLatencyMs, 0, 500);
        
        GUILayout.Label($"Jitter: {simulatedJitterMs}ms", _labelStyle);
        simulatedJitterMs = (int)GUILayout.HorizontalSlider(simulatedJitterMs, 0, 100);
        
        GUILayout.Label($"Packet Loss: {simulatedPacketLossPercent}%", _labelStyle);
        simulatedPacketLossPercent = (int)GUILayout.HorizontalSlider(simulatedPacketLossPercent, 0, 50);
        
        GUILayout.Space(10);
        GUILayout.Label("─── Presets ───", _labelStyle);
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Perfect", _buttonStyle)) ApplyPreset(NetworkPreset.Perfect);
        if (GUILayout.Button("LAN", _buttonStyle)) ApplyPreset(NetworkPreset.LAN);
        if (GUILayout.Button("WiFi", _buttonStyle)) ApplyPreset(NetworkPreset.WiFi);
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("4G", _buttonStyle)) ApplyPreset(NetworkPreset.Mobile4G);
        if (GUILayout.Button("3G", _buttonStyle)) ApplyPreset(NetworkPreset.Mobile3G);
        if (GUILayout.Button("Bad", _buttonStyle)) ApplyPreset(NetworkPreset.BadConnection);
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();
        
        GUI.DragWindow();
    }
}
