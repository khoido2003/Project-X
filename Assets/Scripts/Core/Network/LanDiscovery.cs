using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class LANDiscovery : MonoBehaviour
{
    public static LANDiscovery Instance { get; private set; }
    public List<RoomInfo> rooms = new();
    public event Action OnRoomsUpdated;

    private UdpClient udp;
    private IPEndPoint broadcastEndPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartDiscovery();
        InvokeRepeating(nameof(BroadcastIfServer), 3f, 3f);
    }

    private void BroadcastIfServer()
    {
        if (GameSession.Instance != null)
        {
            BroadcastRoom(
                GameSession.Instance.roomName,
                GameSession.Instance.playerChoices.Count,
                GameSession.Instance.maxPlayers
            );
        }
    }

    public void StartDiscovery()
    {
        if (udp != null)
        {
            Debug.Log("[LANDiscovery] Already running, skipping reinitialization.");
            return;
        }
        try
        {
            int port = 8888;
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));

            broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, 8888);

            StartCoroutine(DiscoveryLoop());
            ListenForRooms();

            Debug.Log("[LANDiscovery] Discovery started on port " + port);
        }
        catch (Exception ex)
        {
            Debug.LogError($"LANDiscovery: failed to start discovery: {ex}");
        }
    }

    private IEnumerator DiscoveryLoop()
    {
        while (true)
        {
            if (udp != null)
            {
                byte[] request = Encoding.UTF8.GetBytes("FIND_ROOMS");
                try
                {
                    udp.Send(request, request.Length, broadcastEndPoint);
                }
                catch (Exception) { }
            }
            yield return new WaitForSeconds(2f);
        }
    }

    public async void ListenForRooms()
    {
        if (udp == null)
        {
            Debug.LogWarning("LANDiscovery: udp not initialized");
            return;
        }

        while (true)
        {
            try
            {
                var result = await udp.ReceiveAsync();
                string msg = Encoding.UTF8.GetString(result.Buffer);
                var remoteEP = result.RemoteEndPoint;

                if (msg.StartsWith("ROOM:"))
                {
                    string[] parts = msg.Substring(5).Split('|');
                    if (parts.Length >= 3)
                    {
                        RoomInfo room = new()
                        {
                            name = parts[0],
                            ip = remoteEP.Address.ToString(),
                            players = int.TryParse(parts[1], out var p) ? p : 0,
                            maxPlayers = int.TryParse(parts[2], out var m) ? m : 0,
                        };

                        var existing = rooms.FirstOrDefault(r => r.ip == room.ip);
                        if (existing != null)
                            rooms.Remove(existing);
                        rooms.Add(room);
                        OnRoomsUpdated?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"LANDiscovery: ListenForRooms error: {ex}");
                await System.Threading.Tasks.Task.Delay(500);
            }
        }
    }

    public static void BroadcastRoom(string name, int players, int max)
    {
        if (Instance == null || Instance.broadcastEndPoint == null)
            return;

        try
        {
            using (UdpClient sender = new UdpClient())
            {
                sender.EnableBroadcast = true;
                string msg = $"ROOM:{name}|{players}|{max}";
                byte[] data = Encoding.UTF8.GetBytes(msg);
                sender.Send(data, data.Length, Instance.broadcastEndPoint);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"LANDiscovery BroadcastRoom failed: {ex.Message}");
        }
    }
}
