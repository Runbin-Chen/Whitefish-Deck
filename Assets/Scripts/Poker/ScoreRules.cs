using System.Collections.Generic;

namespace Whitefish.Poker
{
    /// <summary>
    /// 钓红点 scoring, kept as pure logic so it can be checked without a scene.
    ///
    /// Only red cards count — hearts and diamonds. Black cards are worth nothing however many
    /// you take. A is 20, 2 through 8 score their face value, and 9, 10, J, Q, K are 10 each.
    /// </summary>
    public static class ScoreRules
    {
        /// <summary>Every red card in the deck: 105 per red suit, so 210 in total.</summary>
        public const int MaxScore = 210;

        /// <summary>Red cards score; black ones never do.</summary>
        public static bool Scores(Card card) => card.IsRed;

        public static int Value(Card card)
        {
            if (!card.IsRed) return 0;

            return card.Rank switch
            {
                Rank.Ace => 20,
                Rank.Nine or Rank.Ten or Rank.Jack or Rank.Queen or Rank.King => 10,
                _ => (int)card.Rank, // 2..8 score their face value
            };
        }

        public static int Total(IEnumerable<Card> cards)
        {
            int total = 0;
            foreach (Card card in cards) total += Value(card);
            return total;
        }

        /// <summary>Scoring cards held, for a "12 red" style readout next to the number.</summary>
        public static int CountScoring(IEnumerable<Card> cards)
        {
            int count = 0;
            foreach (Card card in cards)
                if (Scores(card)) count++;
            return count;
        }

        /// <summary>Total for a zone of card views — what the capture pile needs.</summary>
        public static int Total(IReadOnlyList<CardView> views)
        {
            int total = 0;
            for (int i = 0; i < views.Count; i++)
                if (views[i] != null) total += Value(views[i].Card);
            return total;
        }

        public static int CountScoring(IReadOnlyList<CardView> views)
        {
            int count = 0;
            for (int i = 0; i < views.Count; i++)
                if (views[i] != null && Scores(views[i].Card)) count++;
            return count;
        }
    }
}
