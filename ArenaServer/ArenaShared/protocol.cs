// ArenaShared/Net/Messages.cs
namespace ArenaShared.Net;

public enum OpCode : ushort { Hello=1, HelloAck=2, Ping=3, Pong=4, Move=10, Snapshot=11 }
public readonly record struct Vec2(float X, float Y);

public record NetEnvelope(OpCode Op, byte[] Payload, int Version = 1);

// DTOs
public record Hello(string ClientVersion, string PlayerName);
public record HelloAck(Guid SessionId);
public record Ping(long Ticks);
public record Pong(long Ticks);
public record Move(Guid PlayerId, Vec2 Position, Vec2 Direction);
public record PlayerState(Guid Id, Vec2 Position);
public record Snapshot(long Tick, IReadOnlyList<PlayerState> Players);