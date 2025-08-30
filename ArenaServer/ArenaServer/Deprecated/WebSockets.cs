// using ArenaShared.Net;
// using System.Net.WebSockets;
// using System.Text.Json;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Hosting;
//
// var builder = WebApplication.CreateBuilder(args);
//
// var port = Environment.GetEnvironmentVariable("ARENA_PORT") is { } s && int.TryParse(s, out var result) ? result : 5000;
// builder.WebHost.UseKestrel(o => o.ListenAnyIP(port));
//
// var app = builder.Build();
//
// app.Lifetime.ApplicationStarted.Register(() =>
// {
//     Console.WriteLine($"ArenaServer running on ws://0.0.0.0:{port}/ws (Ctrl+C to stop)");
// });
//
// app.UseWebSockets();
// app.Map("/ws", async ctx =>
// {
//     if (!ctx.WebSockets.IsWebSocketRequest)
//     {
//         ctx.Response.StatusCode = 400;
//         return;
//     }
//
//     using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
//
//     var sessionId = Guid.NewGuid();
//     var buf = new byte[64 * 1024];
//     
//     var res = await ws.ReceiveAsync(buf, CancellationToken.None);
//     if (res.MessageType == WebSocketMessageType.Close) return;
//
//     var env = JsonSerializer.Deserialize<NetEnvelope>(buf.AsSpan(0, res.Count))!;
//     if (env.Op != OpCode.Hello)
//     {
//         await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Expected Hello", CancellationToken.None);
//         return;
//     }
//
//     var hello = JsonSerializer.Deserialize<Hello>(env.Payload)!;
//     Console.WriteLine($"HELLO from {hello.PlayerName} v{hello.ClientVersion} -> {sessionId}");
//     
//     var ack = new NetEnvelope(OpCode.HelloAck, JsonSerializer.SerializeToUtf8Bytes(new HelloAck(sessionId)));
//     await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(ack), WebSocketMessageType.Text, true, CancellationToken.None);
//     
//     // Loop, reply to ping wiht pong
//     while (ws.State == WebSocketState.Open)
//     {
//         res = await ws.ReceiveAsync(buf, CancellationToken.None);
//         if (res.MessageType == WebSocketMessageType.Close) break;
//
//         env = JsonSerializer.Deserialize<NetEnvelope>(buf.AsSpan(0, res.Count))!;
//         switch (env.Op)
//         {
//             case OpCode.Ping:
//                 var pong = new NetEnvelope(OpCode.Pong, env.Payload);
//                 await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(pong), WebSocketMessageType.Text, true, CancellationToken.None);
//                 break;
//             
//             // TODO: Handle move -> update world, periodically broadcast snapshot
//         }
//     }
//     
//     Console.WriteLine($"Session {sessionId} disconnected");
// });
//
// await app.RunAsync();