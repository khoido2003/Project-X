using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network Test Manager - Component tổng hợp để dễ dàng setup testing.
/// 
/// Cách sử dụng trong Unity:
/// 1. Tạo GameObject mới trong scene Game
/// 2. Đặt tên: "NetworkDebugManager"
/// 3. Gắn script này vào
/// 4. Nhấn "Setup All Components" trong context menu
/// 
/// Hoặc sử dụng menu: GameObject → Network → Create Network Debug Manager
/// 
/// Khi chạy game:
/// - F3: Toggle Network Stats UI
/// - F4: Toggle Network Condition Simulator
/// - R (khi stats UI mở): Reset statistics
/// </summary>
public class NetworkTestManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private NetworkStatsDebugUI statsUI;
    [SerializeField] private NetworkConditionSimulator conditionSimulator;
    
    [Header("Settings")]
    [SerializeField] private bool autoSetup = true;
    [SerializeField] private bool persistAcrossScenes = true;
    
    private static NetworkTestManager _instance;
    
    private void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
        
        if (autoSetup)
        {
            SetupComponents();
        }
    }
    
    [ContextMenu("Setup All Components")]
    public void SetupComponents()
    {
        // Add NetworkStatsDebugUI
        if (statsUI == null)
        {
            statsUI = GetComponent<NetworkStatsDebugUI>();
            if (statsUI == null)
            {
                statsUI = gameObject.AddComponent<NetworkStatsDebugUI>();
            }
        }
        
        // Add NetworkConditionSimulator
        if (conditionSimulator == null)
        {
            conditionSimulator = GetComponent<NetworkConditionSimulator>();
            if (conditionSimulator == null)
            {
                conditionSimulator = gameObject.AddComponent<NetworkConditionSimulator>();
            }
        }
        
        Debug.Log("[NetworkTestManager] All components setup complete!");
    }
    
    /// <summary>
    /// Get current network quality rating (1-5 stars)
    /// </summary>
    public int GetQualityRating()
    {
        if (NetworkLatencyTester.Instance == null)
            return 0;
            
        float avgRTT = NetworkLatencyTester.Instance.AverageRTT;
        float packetLoss = NetworkLatencyTester.Instance.PacketLoss;
        
        if (avgRTT <= 30 && packetLoss < 1) return 5;
        if (avgRTT <= 50 && packetLoss < 2) return 4;
        if (avgRTT <= 100 && packetLoss < 5) return 3;
        if (avgRTT <= 150 && packetLoss < 10) return 2;
        return 1;
    }
    
    /// <summary>
    /// Log current network stats to console (useful for thesis demo)
    /// </summary>
    [ContextMenu("Log Network Stats")]
    public void LogNetworkStats()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("[NetworkTestManager] NetworkManager not found!");
            return;
        }
        
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log("         NETWORK STATS REPORT              ");
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log($"Connection Status: {(NetworkManager.Singleton.IsConnectedClient ? "Connected" : "Disconnected")}");
        Debug.Log($"Is Server: {NetworkManager.Singleton.IsServer}");
        Debug.Log($"Is Host: {NetworkManager.Singleton.IsHost}");
        Debug.Log($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
        
        if (NetworkLatencyTester.Instance != null)
        {
            var tester = NetworkLatencyTester.Instance;
            Debug.Log("───────────────────────────────────────────");
            Debug.Log($"Current RTT: {tester.CurrentRTT:F1}ms");
            Debug.Log($"Average RTT: {tester.AverageRTT:F1}ms");
            Debug.Log($"Min RTT: {tester.MinRTT:F1}ms");
            Debug.Log($"Max RTT: {tester.MaxRTT:F1}ms");
            Debug.Log($"Jitter: {tester.Jitter:F1}ms");
            Debug.Log($"Packet Loss: {tester.PacketLoss:F1}%");
            Debug.Log($"Quality Rating: {GetQualityRating()}/5 ⭐");
        }
        
        Debug.Log("═══════════════════════════════════════════");
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Menu item to create Network Debug Manager in scene
    /// </summary>
    [UnityEditor.MenuItem("GameObject/Network/Create Network Debug Manager", false, 10)]
    private static void CreateNetworkDebugManager()
    {
        GameObject go = new GameObject("NetworkDebugManager");
        go.AddComponent<NetworkTestManager>();
        
        UnityEditor.Selection.activeGameObject = go;
        Debug.Log("[NetworkTestManager] Created NetworkDebugManager GameObject");
    }
#endif
}
