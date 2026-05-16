using System;
using System.Collections.Generic;
using System.Linq;
using BlackjackServer;

namespace BlackjackServer.Services;

public class BlackjackService
{
    public void InitializeRound(GameRoom room)
    {
        room.Deck = Card.CreateDeck().ToList();
        room.DealerHand = new List<Card>();
        
        foreach (var player in room.Players)
        {
            player.ClearHand();
            player.CurrentBet = 0;
        }
        
        room.State = GameState.Betting;
    }
    
    public void DealCards(GameRoom room)
    {
        foreach (var player in room.Players)
        {
            player.Hand.Add(DrawCard(room));
            player.Hand.Add(DrawCard(room));
        }
        
        room.DealerHand.Add(DrawCard(room));
        room.DealerHand.Add(DrawCard(room));
        
        room.State = GameState.Playing;
        room.CurrentPlayerId = room.Players.First().ConnectionId;
    }
    
    public Card DrawCard(GameRoom room)
    {
        if (room.Deck.Count < 10)
        {
            room.Deck = Card.CreateDeck().ToList();
        }
        
        var card = room.Deck.First();
        room.Deck.RemoveAt(0);
        return card;
    }
    
    public int CalculateDealerPoints(GameRoom room)
    {
        var sum = room.DealerHand.Sum(c => c.Value);
        var aces = room.DealerHand.Count(c => c.Rank == "A");
        
        while (sum > 21 && aces > 0)
        {
            sum -= 10;
            aces--;
        }
        return sum;
    }
    
    public void DealerPlay(GameRoom room)
    {
        while (CalculateDealerPoints(room) < 17)
        {
            room.DealerHand.Add(DrawCard(room));
        }
    }
    
    public (int payout, string result) SettleBet(Player player, int dealerPoints)
    {
        var playerPoints = player.CalculatePoints();
        
        if (playerPoints > 21)
            return (0, "Перебор! Вы проиграли.");
        
        if (dealerPoints > 21)
            return (player.CurrentBet * 2, "Дилер перебрал! Вы выиграли!");
        
        if (player.HasBlackjack && dealerPoints != 21)
            return ((int)(player.CurrentBet * 2.5), "BLACK JACK!");
        
        if (playerPoints > dealerPoints)
            return (player.CurrentBet * 2, "Вы выиграли!");
        
        if (playerPoints < dealerPoints)
            return (0, "Дилер выиграл.");
        
        return (player.CurrentBet, "Ничья. Ставка возвращена.");
    }
}
