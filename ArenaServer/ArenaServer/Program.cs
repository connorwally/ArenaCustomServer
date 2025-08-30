using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;

public sealed class GameServer : INetEventListener
{
    private readonly NetManager _server;
    private readonly int _port;
    private readonly CancellationToken _stopToken;

    // Track connected peers
    private readonly ConcurrentDictionary<NetPeer, Guid> _peerSessions = new();
    
    // Resuable writer to avoid per-send allocations
    private readonly NetDataWriter _writer = new();

    public GameServer(int port, CancellationToken stopToken)
    {
        _port = port;
        _stopToken = stopToken;
        MessagePackSerializer.DefaultOptions =
            MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.StandardResolver.Instance);

        _server = new NetManager(this);
    }

    public void Start()
    {
        if (!_server.Start(_port))
        {
            throw new Exception($"Failed to bind UDP port {_port}");
        }
        
        Console.WriteLine($"ArenaServer (UDP) listening on udp://0.0.0.0:{_port} (Ctrl+C to stop)");
        
        // Main loop: pump events + example 30 Hz tick
        const int tickRate = 30;
        var tickIntervalMs = 1000 / tickRate;
        var sw = Stopwatch.StartNew();
        long nextTickAt = sw.ElapsedMilliseconds + tickIntervalMs;

        while (!_stopToken.IsCancellationRequested)
        {
            _server.PollEvents();
            
            // Tick @ 30 Hz
            var now = sw.ElapsedMilliseconds;
            if (now >= nextTickAt)
            {
                // TODO: Advance world, broadcast snapshots etc.
                nextTickAt += tickIntervalMs;
            }

            Thread.Sleep(1);
        }
        
        Console.WriteLine("Shutting down server...");
        _server.Stop();
    }

    // ----------------------------------------------------------------------------------- INetEventListener

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.Accept();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"Peer connected: {peer.Address}:{peer.Port}");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (_peerSessions.TryRemove(peer, out var sessionId))
        {
            Console.WriteLine($"Session {sessionId} disconnected ({disconnectInfo.Reason})");
        }
        else
        {
            Console.WriteLine($"Peer {peer.Address}:{peer.Port} disconnected ({disconnectInfo.Reason})");
        }
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Console.WriteLine($"Network error {socketError} from {endPoint}");
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        try
        {
            var data = reader.GetRemainingBytes();
            reader.Recycle();

            var env = MessagePackSerializer.Deserialize<NetEnvelope>(data);

            switch (env.Op)
            {
                case OpCode.Hello:
                {
                    var hello = MessagePackSerializer.Deserialize<Hello>(env.Payload);

                    var sessionId = Guid.NewGuid();
                    _peerSessions[peer] = sessionId;

                    Console.WriteLine($"HELLO from {hello.PlayerName} v{hello.ClientVersion} -> {sessionId}");

                    var ack = new NetEnvelope(OpCode.HelloAck,
                        MessagePackSerializer.Serialize(new HelloAck{SessionId = sessionId}));

                    SendEnvelope(peer, ack, DeliveryMethod.ReliableOrdered);
                    break;
                }

                case OpCode.Ping:
                {
                    var pong = new NetEnvelope(OpCode.Pong, env.Payload);
                    SendEnvelope(peer, pong, DeliveryMethod.ReliableOrdered);
                    break;
                }
                case OpCode.HelloAck:
                case OpCode.Pong:
                case OpCode.Move:
                case OpCode.Snapshot:
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receive error: {ex}");
        }
    }

    // ----------------------------------------------------------------------------------- HELPERS

    private void SendEnvelope(NetPeer peer, NetEnvelope env, DeliveryMethod method)
    {
        var bytes = MessagePackSerializer.Serialize(env);
        
        _writer.Reset();
        _writer.Put(bytes);
        peer.Send(_writer, channelNumber: 0, deliveryMethod: method);
    }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var port = TryParseEnv("ARENA_PORT", 7979);
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var server = new GameServer(port, cts.Token);
        server.Start();

        while (!cts.IsCancellationRequested)
            await Task.Delay(100, cts.Token).ContinueWith(_ => { }, cts.Token);
    }

    private static int TryParseEnv(string name, int fallback) =>
        Environment.GetEnvironmentVariable(name) is { } s && int.TryParse(s, out var v) ? v : fallback;
}