using System.Collections.Generic;
using System.Linq;

namespace BlackjackServer;

public class Player
{
    public string ConnectionId { get; set; } = "";
    public string Name { get; set; } = "Игрок";
    public List<Card> Hand { get; set; } = new();
    public int Balance { get; set; } = 100;
    public int CurrentBet { get; set; }
    public bool IsStanding { get; set; }
    
    public int CalculatePoints()
    {
        var sum = Hand.Sum(c => c.Value);
        var aces = Hand.Count(c => c.Rank == "A");
        
        while (sum > 21 && aces > 0)
        {
            sum -= 10;
            aces--;
        }
        return sum;
    }
    
    public bool HasBlackjack => Hand.Count == 2 && CalculatePoints() == 21;
    
    public void ClearHand()
    {
        Hand.Clear();
        IsStanding = false;
    }
}
