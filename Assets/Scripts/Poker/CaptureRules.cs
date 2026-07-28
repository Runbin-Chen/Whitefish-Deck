namespace Whitefish.Poker
{
    /// <summary>
    /// 钓红点 capture rules, kept as pure logic so they can be checked without a scene.
    ///
    /// Two cards are taken together when they either pair to ten — A counts as 1, giving
    /// A-9, 2-8, 3-7, 4-6, 5-5 — or when both are the same rank among 10, J, Q, K.
    /// The two paths never overlap: a ten or a face card can only ever take its own rank.
    /// </summary>
    public static class CaptureRules
    {
        public const int TargetSum = 10;

        /// <summary>10, J, Q and K do not pair to ten; they capture their own rank instead.</summary>
        public static bool MatchesByRank(Rank rank) =>
            rank == Rank.Ten || rank == Rank.Jack || rank == Rank.Queen || rank == Rank.King;

        /// <summary>Pip value used for pairing. Ace counts as 1; only A..9 take part.</summary>
        public static int PipValue(Rank rank) => rank == Rank.Ace ? 1 : (int)rank;

        public static bool CanCapture(Card a, Card b)
        {
            // If either side is a ten or a face card, rank matching is the only way through —
            // otherwise a 10 would "pair" with a 0 that does not exist.
            if (MatchesByRank(a.Rank) || MatchesByRank(b.Rank))
                return a.Rank == b.Rank;

            return PipValue(a.Rank) + PipValue(b.Rank) == TargetSum;
        }

        /// <summary>Why a pair does or does not capture. For logs and debugging.</summary>
        public static string Explain(Card a, Card b)
        {
            if (MatchesByRank(a.Rank) || MatchesByRank(b.Rank))
            {
                return a.Rank == b.Rank
                    ? $"{a} 对碰 {b}"
                    : $"{a} 与 {b} 点数不同,无法对碰";
            }

            int sum = PipValue(a.Rank) + PipValue(b.Rank);
            return sum == TargetSum
                ? $"{a} + {b} = 10"
                : $"{a} + {b} = {sum},不是 10";
        }
    }
}
