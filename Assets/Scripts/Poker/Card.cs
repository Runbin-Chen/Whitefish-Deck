using System;

namespace Whitefish.Poker
{
    public enum Suit
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3,
    }

    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14,
    }

    /// <summary>
    /// A single playing card. A value type, so it is cheap to copy and safe to hand around
    /// without worrying about who owns it.
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        public const int DeckSize = 52;
        public const int RanksPerSuit = 13;

        public readonly Suit Suit;
        public readonly Rank Rank;

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public bool IsRed => Suit == Suit.Diamonds || Suit == Suit.Hearts;

        /// <summary>Suit-major index in 0..51. Matches the sprite order in <see cref="CardSpriteLibrary"/>.</summary>
        public int Index => (int)Suit * RanksPerSuit + ((int)Rank - 2);

        public static Card FromIndex(int index) =>
            new Card((Suit)(index / RanksPerSuit), (Rank)(index % RanksPerSuit + 2));

        public string RankLabel => Rank switch
        {
            Rank.Ace => "A",
            Rank.King => "K",
            Rank.Queen => "Q",
            Rank.Jack => "J",
            _ => ((int)Rank).ToString(),
        };

        public string SuitLabel => Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            _ => "♠",
        };

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => RankLabel + SuitLabel;

        public static bool operator ==(Card a, Card b) => a.Equals(b);
        public static bool operator !=(Card a, Card b) => !a.Equals(b);
    }
}
