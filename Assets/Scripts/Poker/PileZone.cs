using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// The capture pile in the bottom-left corner. Only the most recent few pairs are spread out;
    /// everything older collapses into a thin stack buried behind them, so the pile stays the same
    /// size whether you have taken two pairs or twenty.
    ///
    /// Rows run upward as they age, which is what keeps them readable: a newer row sits *below* an
    /// older one and covers its bottom edge, leaving the top-left rank corner of every card showing.
    ///
    ///     (older pairs, collapsed)
    ///     4 6      <- oldest visible
    ///     2 8
    ///     A 9      <- newest, drawn on top
    ///
    /// Cards arrive in capture order — played hand card first, then the table card it took.
    /// Unlike <see cref="DeckZone"/> this keeps real <see cref="CardView"/>s, because captured
    /// cards have to be counted at the end of the round.
    /// </summary>
    public class PileZone : CardZone
    {
        [Header("Spread")]
        [Tooltip("How many of the newest pairs stay spread out and readable.")]
        [Min(1)]
        [SerializeField] int visibleRows = 3;

        [Tooltip("Horizontal step between the two cards of a pair.")]
        [SerializeField] float pairSpacing = 0.3f;

        [Tooltip("Vertical step between spread rows. Small enough to overlap, big enough to read.")]
        [SerializeField] float rowSpacing = 0.32f;

        [Header("Buried stack")]
        [Tooltip("Per-pair offset for collapsed rows, giving the buried stack some thickness.")]
        [SerializeField] Vector2 collapsedStep = new Vector2(0.5f / 64f, 1f / 64f);

        [Tooltip("The buried stack stops thickening past this many pairs.")]
        [Min(0)]
        [SerializeField] int maxCollapsedSteps = 8;

        [Header("Looseness")]
        [Tooltip("Random nudge per card, so the pairs look tossed down rather than filed.")]
        [SerializeField] float positionJitter = 0.045f;
        [SerializeField] float rotationJitter = 6f;
        [SerializeField] int scatterSeed = 8123;

        [Tooltip("Captured cards are shown face up so the red ones stay countable.")]
        [SerializeField] bool faceUp = true;

        /// <summary>Rows currently held. The last one is half-empty if a card arrives unpaired.</summary>
        public int PairCount => (cards.Count + 1) / 2;

        public override void Add(CardView card, bool instant = false)
        {
            base.Add(card, instant);

            if (card == null) return;
            card.SetSelected(false);
            card.SetHighlighted(false);
            card.SetFaceUp(faceUp);
        }

        public override void Relayout(bool instant = false)
        {
            int rows = PairCount;
            if (rows == 0) return;

            int firstSpreadRow = Mathf.Max(0, rows - visibleRows);
            float topSlotY = (visibleRows - 1) * rowSpacing;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;

                int row = i / 2;
                int column = i % 2;
                var position = new Vector2(pairSpacing * column, 0f);

                if (row >= firstSpreadRow)
                {
                    // Spread: the newest row sits at the anchor, older ones climb above it.
                    position.y = (rows - 1 - row) * rowSpacing;
                }
                else
                {
                    // Buried: parked at the top slot, offset a little by how deep it is.
                    int depth = Mathf.Min(firstSpreadRow - row, maxCollapsedSteps);
                    position.x += collapsedStep.x * depth;
                    position.y = topSlotY + collapsedStep.y * depth;
                }

                position.x += Jitter(i, 0) * positionJitter;
                position.y += Jitter(i, 1) * positionJitter;
                float rotation = Jitter(i, 2) * rotationJitter;

                // Sorting by arrival index gives every overlap for free: newer rows draw over
                // older ones, spread rows draw over the buried stack, and a pair's right card
                // draws over the left card's right edge.
                cards[i].SetPose(new CardPose(position, rotation, baseSortingOrder + i), instant);
            }
        }

        /// <summary>Stable pseudo-random in -1..1 per slot, so the pile never reshuffles itself.</summary>
        float Jitter(int slot, int channel)
        {
            unchecked
            {
                uint h = (uint)(scatterSeed * 73856093) ^ (uint)(slot * 19349663) ^ (uint)(channel * 83492791);
                h ^= h >> 13;
                h *= 0x85EBCA6B;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        void OnDrawGizmosSelected()
        {
            float height = (visibleRows - 1) * rowSpacing;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(pairSpacing * 0.5f, height * 0.5f, 0f),
                new Vector3(pairSpacing + 0.65625f, height + 0.9375f, 0f));
        }
    }
}
