using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;
using UnityEngine;

public class Client : MonoBehaviour, INetEventListener
{
    [Header("Connection")] 
    public string host = "127.0.0.1";
    public int port = 7979;

    [Header("Identity")] 
    public string username = "Wally";
    public string clientVersion = "0.1.0";

    [Header("Ping")] 
    public float pingIntervalSeconds = 2f;

    private NetManager _client;
    private NetPeer _serverPeer;
    private readonly NetDataWriter _writer = new();
    private float _nextPingAt;
    private Guid _sessionId = Guid.Empty;
    private int _lastRtt;
    private bool _connected;
    private bool _helloAcked;

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _logs = new();

    private void Start()
    {
        Application.runInBackground = true;

        MessagePackSerializer.DefaultOptions =
            MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.StandardResolver.Instance);
        
        _client = new NetManager(this);

        if (!_client.Start())
        {
            Debug.LogError("Failed to start NetManager (client).");
            return;
        }

        Log($"Connecting to  {host}:{port}...");
        _client.Connect(host, port, string.Empty);
    }

    private void Update()
    {
        _client?.PollEvents();

        if (_connected && _helloAcked && Time.time >= _nextPingAt)
        {
            SendPing();
            _nextPingAt = Time.time + pingIntervalSeconds;
        }

        while (_logs.TryDequeue(out var line))
        {
            Debug.Log(line);
        }
    }

    private void OnApplicationQuit()
    {
        try { _client?.Stop(); }
        catch
        {
            // ignored
        }
    }

    // ----------------------------------------------------------------------------------- INetEventListener

    public void OnPeerConnected(NetPeer peer)
    {
        _serverPeer = peer;
        _connected = true;
        Log($"[Client] Connected: {peer.Address}:{peer.Port}. Sending Hello...");
        var hello = new Hello(username, clientVersion);
        var helloBytes = MessagePackSerializer.Serialize(hello);
        var env = new NetEnvelope(OpCode.Hello, helloBytes);
        SendEnvelope(env, DeliveryMethod.ReliableOrdered);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Log($"[Client] Disconnected: {disconnectInfo.Reason}");
        _connected = false;
        _helloAcked = false;
        _sessionId = Guid.Empty;
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) => Log($"[Client] Network error {socketError} from {endPoint}.");

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) => _lastRtt = latency;

    public void OnConnectionRequest(ConnectionRequest request) { } // Client doesnt receive these.

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        try
        {
            var data = reader.GetRemainingBytes();
            reader.Recycle();
        
            var env = MessagePackSerializer.Deserialize<NetEnvelope>(data);
            if (env == null) return;
        
            switch (env.Op)
            {
                case OpCode.HelloAck:
                {
                    var ack = MessagePackSerializer.Deserialize<HelloAck>(env.Payload);
                    if (ack == null) return;
        
                    _sessionId = ack.SessionId;
                    _helloAcked = true;
                    _nextPingAt = Time.time + pingIntervalSeconds;
                    Log($"[Client] HelloAck received. SessionId: {_sessionId}");
                    break;
                }
                case OpCode.Pong:
                {
                    Log($"[Client] Pong received. rtt={_lastRtt}ms");
                    break;
                }
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"[Client] Receive error: {ex}");
        }
    }

    // ----------------------------------------------------------------------------------- HELPERS

    private void SendEnvelope(NetEnvelope env, DeliveryMethod method)
    {
        if (_serverPeer is not { ConnectionState: ConnectionState.Connected }) return;
    
        var bytes = MessagePackSerializer.Serialize(env);
        _writer.Reset();
        _writer.Put(bytes);
        _serverPeer.Send(_writer, channelNumber: 0, deliveryMethod: method);
    }
    
    private void SendPing()
    {
        var pingPayload = MessagePackSerializer.Serialize(new Ping(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        var ping = new NetEnvelope(OpCode.Ping, pingPayload);
        SendEnvelope(ping, DeliveryMethod.ReliableOrdered);
        Log("[Client] Ping sent");
    }
    
    private void Log(string msg) => _logs.Enqueue(msg);
}
