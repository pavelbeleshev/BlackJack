using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackjackServer;
using BlackjackServer.Services;
using BlackjackServer.Models;
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
    
    public async Task MakeBet(int amount)
    {
        Console.WriteLine($"MakeBet вызван: amount={amount}, connectionId={Context.ConnectionId}");
        
        var room = FindRoomByConnectionId(Context.ConnectionId);
        if (room == null)
        {
            await Clients.Caller.SendAsync("Error", "Комната не найдена");
            return;
        }
        
        var player = room.GetPlayer(Context.ConnectionId);
        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", "Игрок не найден");
            return;
        }
        
        if (amount > player.Balance)
        {
            await Clients.Caller.SendAsync("Error", "Не хватает фишек");
            return;
        }
        
        player.CurrentBet = amount;
        player.Balance -= amount;
        
        await Clients.Group(room.RoomId).SendAsync("BetPlaced", player.Name, amount, player.Balance);
        await Clients.Caller.SendAsync("BetConfirmed", amount);
        
        if (room.Players.All(p => p.CurrentBet > 0))
        {
            _game.DealCards(room);
            
            foreach (var p in room.Players)
            {
                await Clients.Client(p.ConnectionId).SendAsync("GameDealt", new
                {
                    playerHand = p.Hand.Select(c => c.Display),
                    playerPoints = p.CalculatePoints(),
                    dealerFirstCard = room.DealerHand.First().Display,
                    balance = p.Balance,
                    hasBlackjack = p.HasBlackjack
                });
            }
            
            room.CurrentPlayerId = room.Players.First().ConnectionId;
            await Clients.Group(room.RoomId).SendAsync("CurrentTurn", room.CurrentPlayerId);
        }
    }
    
    public async Task Hit()
    {
        Console.WriteLine($"Hit вызван: connectionId={Context.ConnectionId}");
        
        var room = FindRoomByConnectionId(Context.ConnectionId);
        if (room == null) return;
        
        var player = room.GetPlayer(Context.ConnectionId);
        if (player == null) return;
        
        if (room.CurrentPlayerId != Context.ConnectionId)
        {
            await Clients.Caller.SendAsync("Error", "Сейчас не ваш ход");
            return;
        }
        
        var card = _game.DrawCard(room);
        player.Hand.Add(card);
        var points = player.CalculatePoints();
        
        await Clients.Group(room.RoomId).SendAsync("PlayerHit", player.Name, card.Display, points);
        await Clients.Caller.SendAsync("UpdateYourHand", new
        {
            hand = player.Hand.Select(c => c.Display),
            points = points
        });
        
        if (points > 21)
        {
            await Clients.Group(room.RoomId).SendAsync("PlayerBusted", player.Name);
            player.IsStanding = true;
            await NextPlayer(room);
        }
    }
    
    public async Task Stand()
    {
        Console.WriteLine($"Stand вызван: connectionId={Context.ConnectionId}");
        
        var room = FindRoomByConnectionId(Context.ConnectionId);
        if (room == null) return;
        
        var player = room.GetPlayer(Context.ConnectionId);
        if (player == null) return;
        
        if (room.CurrentPlayerId != Context.ConnectionId)
        {
            await Clients.Caller.SendAsync("Error", "Сейчас не ваш ход");
            return;
        }
        
        player.IsStanding = true;
        await Clients.Group(room.RoomId).SendAsync("PlayerStand", player.Name);
        await NextPlayer(room);
    }
    
    private async Task NextPlayer(GameRoom room)
    {
        var currentIndex = room.Players.FindIndex(p => p.ConnectionId == room.CurrentPlayerId);
        Player? nextPlayer = null;
        
        for (int i = currentIndex + 1; i < room.Players.Count; i++)
        {
            if (!room.Players[i].IsStanding && room.Players[i].CalculatePoints() <= 21)
            {
                nextPlayer = room.Players[i];
                break;
            }
        }
        
        if (nextPlayer != null)
        {
            room.CurrentPlayerId = nextPlayer.ConnectionId;
            await Clients.Group(room.RoomId).SendAsync("CurrentTurn", room.CurrentPlayerId);
            await Clients.Client(nextPlayer.ConnectionId).SendAsync("YourTurn");
        }
        else
        {
            await FinishRound(room);
        }
    }
    
    private async Task FinishRound(GameRoom room)
    {
        room.State = GameState.DealerTurn;
        await Clients.Group(room.RoomId).SendAsync("DealerTurn");
        
        _game.DealerPlay(room);
        var dealerPoints = _game.CalculateDealerPoints(room);
        
        await Clients.Group(room.RoomId).SendAsync("DealerReveal", new
        {
            hand = room.DealerHand.Select(c => c.Display),
            points = dealerPoints
        });
        
        var results = new List<object>();
        foreach (var player in room.Players)
        {
            var (payout, message) = _game.SettleBet(player, dealerPoints);
            player.Balance += payout;
            
            results.Add(new
            {
                player.Name,
                Message = message,
                NewBalance = player.Balance,
                Payout = payout
            });
            
            await Clients.Client(player.ConnectionId).SendAsync("YourResult", new
            {
                Message = message,
                NewBalance = player.Balance
            });
        }
        
        await Clients.Group(room.RoomId).SendAsync("RoundResults", results);
        
        await Task.Delay(3000);
        
        _game.InitializeRound(room);
        await Clients.Group(room.RoomId).SendAsync("NewRound", room.State);
    }
    
    private GameRoom? FindRoomByConnectionId(string connectionId)
    {
        return _rooms.Values.FirstOrDefault(r => r.Players.Any(p => p.ConnectionId == connectionId));
    }
    
    private string GenerateRoomId()
    {
        var random = new Random();
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 4)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
