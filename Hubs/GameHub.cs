using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackjackServer;
using BlackjackServer.Services;
using Microsoft.AspNetCore.SignalR;

namespace BlackjackServer.Hubs;

public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly BlackjackService _game = new();
    
    public async Task CreateRoom(string playerName)
    {
        var roomId = GenerateRoomId();
        var player = new Player
        {
            ConnectionId = Context.ConnectionId,
            Name = playerName,
            Balance = 100
        };
        
        var room = new GameRoom
        {
            RoomId = roomId,
            Players = new List<Player> { player },
            State = GameState.Waiting
        };
        
        _rooms.TryAdd(roomId, room);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Caller.SendAsync("RoomCreated", roomId);
    }
    
    public async Task JoinRoom(string roomId, string playerName)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("Error", "Комната не найдена");
            return;
        }
        
        if (room.IsFull)
        {
            await Clients.Caller.SendAsync("Error", "Комната полна");
            return;
        }
        
        var player = new Player
        {
            ConnectionId = Context.ConnectionId,
            Name = playerName,
            Balance = 100
        };
        
        room.Players.Add(player);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        
        await Clients.Group(roomId).SendAsync("GameReady", new
        {
            Players = room.Players.Select(p => new { p.Name, p.Balance }),
            RoomId = roomId
        });
        
        _game.InitializeRound(room);
        await Clients.Group(roomId).SendAsync("RoundStarted");
    }
    
    private string GenerateRoomId()
    {
        var random = new Random();
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 4)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
