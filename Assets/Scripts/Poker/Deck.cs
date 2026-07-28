using System;
using System.Collections.Generic;

namespace Whitefish.Poker
{
    /// <summary>
    /// A draw pile of card data. Pure C# with no scene dependencies, so round logic can be
    /// tested without opening a scene. <see cref="DeckZone"/> is the visual counterpart.
    /// </summary>
    public class Deck
    {
        readonly List<Card> cards = new List<Card>(Card.DeckSize);
        readonly System.Random rng;

        public int Count => cards.Count;
        public bool IsEmpty => cards.Count == 0;
        public IReadOnlyList<Card> Cards => cards;

        /// <summary>Pass a seed to get a reproducible shuffle; leave it null for a fresh one each run.</summary>
        public Deck(int? seed = null)
        {
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public void FillStandard52()
        {
            cards.Clear();
            for (int i = 0; i < Card.DeckSize; i++)
                cards.Add(Card.FromIndex(i));
        }

        /// <summary>Fisher-Yates, biased only by the seed.</summary>
        public void Shuffle()
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        /// <summary>Removes and returns the top card. Throws when empty; prefer <see cref="TryDraw"/>.</summary>
        public Card Draw()
        {
            if (cards.Count == 0)
                throw new InvalidOperationException("Deck is empty.");

            Card top = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            return top;
        }

        public bool TryDraw(out Card card)
        {
            if (cards.Count == 0)
            {
                card = default;
                return false;
            }

            card = Draw();
            return true;
        }

        public void PutOnTop(Card card) => cards.Add(card);
        public void PutOnBottom(Card card) => cards.Insert(0, card);
        public void Clear() => cards.Clear();
    }
}
