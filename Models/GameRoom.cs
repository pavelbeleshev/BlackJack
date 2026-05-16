using System.Collections.Generic;
using System.Linq;

namespace BlackjackServer;

public enum GameState
{
    Waiting,
    Betting,
    Playing,
    DealerTurn,
    Finished
}

public class GameRoom
{
    public string RoomId { get; set; } = "";
    public List<Player> Players { get; set; } = new();
    public List<Card> Deck { get; set; } = new();
    public List<Card> DealerHand { get; set; } = new();
    public GameState State { get; set; } = GameState.Waiting;
    public string CurrentPlayerId { get; set; } = "";
    
    public Player? GetPlayer(string connectionId)
        => Players.FirstOrDefault(p => p.ConnectionId == connectionId);
    
    public bool IsFull => Players.Count >= 2;
    public bool IsEmpty => Players.Count == 0;
}
