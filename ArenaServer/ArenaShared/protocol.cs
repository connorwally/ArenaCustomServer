using MessagePack;

public enum OpCode : ushort { Hello=1, HelloAck=2, Ping=3, Pong=4, Move=10, Snapshot=11 }

[MessagePackObject]
public struct Vec2
{
    public Vec2(float x, float y)
    {
        X = x;
        Y = y;
    }
    [Key(0)] public float X { get; }
    [Key(1)] public float Y { get; }
}

[MessagePackObject]
public class NetEnvelope
{
    [Key(0)] public OpCode Op { get; }
    [Key(1)] public byte[] Payload { get; }
    [Key(2)] public int Version { get; }

    public NetEnvelope(OpCode op, byte[] payload, int version = 1)
    {
        Op = op; Payload = payload; Version = version;
    }
}

[MessagePackObject]
public class Hello
{
    [Key(0)] public readonly string PlayerName;
    [Key(1)] public readonly string ClientVersion;
    public Hello(string playerName, string clientVersion)
    {
        PlayerName = playerName;
        ClientVersion = clientVersion;
    }
    public Hello(string n, string v, string playerName, string clientVersion) { PlayerName = n; ClientVersion = v; }
}

[MessagePackObject]
public class HelloAck
{
    [Key(0)] public Guid SessionId { get; set; }
    public HelloAck() { }
    public HelloAck(Guid id) => SessionId = id;
}

[MessagePackObject]
public class Ping
{
    [Key(0)] public long Ticks { get; set; }
    public Ping() { }
    public Ping(long t) => Ticks = t;
}

[MessagePackObject]
public class Pong
{
    [Key(0)] public long Ticks { get; set; }
    public Pong() { }
    public Pong(long t) => Ticks = t;
}

[MessagePackObject]
public class Move
{
    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public Vec2 Position { get; set; }
    [Key(2)] public Vec2 Direction { get; set; }
}

[MessagePackObject]
public class PlayerState
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public Vec2 Position { get; set; }
}

[MessagePackObject]
public class Snapshot
{
    [Key(0)] public long Tick { get; set; }
    [Key(1)] public PlayerState[] Players { get; set; } = Array.Empty<PlayerState>();
}