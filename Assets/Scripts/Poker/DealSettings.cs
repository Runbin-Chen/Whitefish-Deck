using System;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// How many cards a round opens with. The presets are the counts printed in the rules;
    /// every field is also settable by hand so a mode or a test can deal any split it likes.
    /// </summary>
    [Serializable]
    public struct DealSettings
    {
        [Min(1)]
        [Tooltip("Drives the preset and the draw pile size. Only the local hand is dealt for now.")]
        public int PlayerCount;

        [Min(0)] public int HandCardsPerPlayer;
        [Min(0)] public int TableCards;

        public DealSettings(int playerCount, int handCardsPerPlayer, int tableCards)
        {
            PlayerCount = playerCount;
            HandCardsPerPlayer = handCardsPerPlayer;
            TableCards = tableCards;
        }

        /// <summary>Cards still face down in the draw pile once the deal finishes.</summary>
        public int DrawPileCards => Card.DeckSize - PlayerCount * HandCardsPerPlayer - TableCards;

        public bool IsValid => PlayerCount >= 1 && HandCardsPerPlayer >= 0 && TableCards >= 0 && DrawPileCards >= 0;

        /// <summary>
        /// The official table: 2P 10/12/20, 3P 7/10/21, 4P 5/12/20 — each adds up to 52.
        /// Unlisted player counts fall back to the 4-player split.
        /// </summary>
        public static DealSettings ForPlayers(int playerCount) => playerCount switch
        {
            2 => new DealSettings(2, 10, 12),
            3 => new DealSettings(3, 7, 10),
            4 => new DealSettings(4, 5, 12),
            _ => new DealSettings(Mathf.Max(1, playerCount), 5, 12),
        };

        public override string ToString() =>
            $"{PlayerCount}P: hand {HandCardsPerPlayer} x{PlayerCount}, table {TableCards}, pile {DrawPileCards}";
    }
}
