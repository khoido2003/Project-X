using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network Latency Tester - Đo RTT chính xác bằng custom ping/pong RPC.
/// 
/// Cách hoạt động:
/// 1. Client gửi PingServerRpc với timestamp
/// 2. Server nhận và gửi lại PongClientRpc với timestamp gốc
/// 3. Client tính RTT = now - originalTimestamp
/// 
/// Sử dụng: Tự động chạy khi connected, hiển thị qua NetworkStatsDebugUI
/// </summary>
public class NetworkLatencyTester : NetworkBehaviour
{
    public static NetworkLatencyTester Instance { get; private set; }
    
    [Header("Settings")]
    [SerializeField] private float pingInterval = 0.5f;
    [SerializeField] private int maxHistorySize = 100;
    
    // Public stats
    public float CurrentRTT { get; private set; }
    public float AverageRTT { get; private set; }
    public float MinRTT { get; private set; } = float.MaxValue;
    public float MaxRTT { get; private set; }
    public float Jitter { get; private set; }
    public int PingsSent { get; private set; }
    public int PingsReceived { get; private set; }
    public float PacketLoss => PingsSent > 0 ? (1f - (float)PingsReceived / PingsSent) * 100f : 0f;
    
    // Internal
    private float _lastPingTime;
    private float _pingTimer;
    private readonly Queue<float> _rttHistory = new();
    private float _rttSum;
    private readonly Queue<float> _jitterSamples = new();
    private float _lastRTT;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            ResetStats();
            Debug.Log("[NetworkLatencyTester] Started for local player");
        }
    }
    
    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;
        
        _pingTimer += Time.deltaTime;
        
        if (_pingTimer >= pingInterval)
        {
            _pingTimer = 0f;
            SendPing();
        }
    }
    
    private void SendPing()
    {
        if (!IsOwner) return;
        
        PingsSent++;
        _lastPingTime = Time.realtimeSinceStartup;
        PingServerRpc(_lastPingTime);
    }
    
    [ServerRpc]
    private void PingServerRpc(float clientTimestamp)
    {
        // Server immediately responds with original timestamp
        PongClientRpc(clientTimestamp);
    }
    
    [ClientRpc]
    private void PongClientRpc(float originalTimestamp)
    {
        if (!IsOwner) return;
        
        float rtt = (Time.realtimeSinceStartup - originalTimestamp) * 1000f; // Convert to ms
        
        if (rtt < 0 || rtt > 5000) return; // Sanity check
        
        PingsReceived++;
        ProcessRTT(rtt);
    }
    
    private void ProcessRTT(float rtt)
    {
        CurrentRTT = rtt;
        
        // Calculate jitter (variation in RTT)
        if (_lastRTT > 0)
        {
            float jitterSample = Mathf.Abs(rtt - _lastRTT);
            _jitterSamples.Enqueue(jitterSample);
            
            if (_jitterSamples.Count > 20)
                _jitterSamples.Dequeue();
                
            float jitterSum = 0f;
            foreach (var j in _jitterSamples)
                jitterSum += j;
            Jitter = jitterSum / _jitterSamples.Count;
        }
        _lastRTT = rtt;
        
        // Track history
        _rttHistory.Enqueue(rtt);
        _rttSum += rtt;
        
        if (_rttHistory.Count > maxHistorySize)
        {
            _rttSum -= _rttHistory.Dequeue();
        }
        
        // Calculate stats
        AverageRTT = _rttSum / _rttHistory.Count;
        
        if (rtt < MinRTT) MinRTT = rtt;
        if (rtt > MaxRTT) MaxRTT = rtt;
    }
    
    public void ResetStats()
    {
        CurrentRTT = 0;
        AverageRTT = 0;
        MinRTT = float.MaxValue;
        MaxRTT = 0;
        Jitter = 0;
        PingsSent = 0;
        PingsReceived = 0;
        _rttHistory.Clear();
        _jitterSamples.Clear();
        _rttSum = 0;
        _lastRTT = 0;
        
        Debug.Log("[NetworkLatencyTester] Stats reset");
    }
    
    /// <summary>
    /// Get formatted stats string for display
    /// </summary>
    public string GetFormattedStats()
    {
        return $"RTT: {CurrentRTT:F1}ms (Avg: {AverageRTT:F1}ms)\n" +
               $"Min: {(MinRTT < float.MaxValue ? MinRTT : 0):F1}ms | Max: {MaxRTT:F1}ms\n" +
               $"Jitter: {Jitter:F1}ms\n" +
               $"Packet Loss: {PacketLoss:F1}%\n" +
               $"Pings: {PingsReceived}/{PingsSent}";
    }
}
