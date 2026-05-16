namespace BlackjackServer.Models;

public class Card
{
    public string Suit { get; set; } = "";
    public string Rank { get; set; } = "";
    public int Value { get; set; }
    
    public string Display => $"{Suit}{Rank}";
    
    public static Card[] CreateDeck()
    {
        var suits = new[] { "♥️", "♦️", "♣️", "♠️" };
        var ranks = new[] { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
        var deck = new List<Card>();
        
        foreach (var suit in suits)
        foreach (var rank in ranks)
        {
            var value = rank switch
            {
                "A" => 11,
                "K" or "Q" or "J" => 10,
                _ => int.Parse(rank)
            };
            deck.Add(new Card { Suit = suit, Rank = rank, Value = value });
        }
        
        return deck.OrderBy(x => Guid.NewGuid()).ToArray();
    }
}