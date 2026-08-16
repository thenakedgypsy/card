using Godot;
using System.Collections.Generic;

public partial class Cardpile : Node2D
{
    // Now stores strings instead of Card nodes
    protected List<string> cardIds = new List<string>();

    public virtual void AddCard(string cardId)
    {
        cardIds.Add(cardId);
    }

    public virtual void RemoveCard(string cardId)
    {
        cardIds.Remove(cardId);
    }

    // Added a helper for drawing
    public virtual string DrawTopCard()
    {
        if (cardIds.Count == 0) return null;
        
        string id = cardIds[cardIds.Count - 1];
        cardIds.RemoveAt(cardIds.Count - 1);
        return id;
    }

    public int GetNumCards()
    {
        return cardIds.Count;
    }

    public bool TryRemoveCard(string cardId)
    {
        if (cardIds.Contains(cardId))
        {
            RemoveCard(cardId);
            return true;
        }
        return false;
    }

    public IReadOnlyList<string> GetCards()
    {
        return cardIds;
    }

    // Add these inside Cardpile.cs

    public void Clear()
    {
        cardIds.Clear();
    }

    public void AddCards(IEnumerable<string> ids)
    {
        cardIds.AddRange(ids);
    }

    public void Shuffle()
    {
        // Standard Fisher-Yates shuffle
        System.Random rng = new System.Random();
        int n = cardIds.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            string value = cardIds[k];
            cardIds[k] = cardIds[n];
            cardIds[n] = value;
        }
    }
}