using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// A single holding spot — used for the card just drawn from the pile, which waits above the
    /// deck while the player decides what to do with it. Keeping it out of
    /// <see cref="TableZone"/> until then means the table never shows a card the player has not
    /// resolved yet.
    /// </summary>
    public class SlotZone : CardZone
    {
        [Tooltip("Offset per card if more than one ever ends up here. Normally there is just one.")]
        [SerializeField] Vector2 stackOffset = new Vector2(0f, 2f / 64f);

        [SerializeField] bool faceUp = true;

        public CardView Top => cards.Count > 0 ? cards[cards.Count - 1] : null;

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
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;
                cards[i].SetPose(
                    new CardPose(stackOffset * i, 0f, baseSortingOrder + i),
                    instant);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 1f, 0.6f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(42f / 64f, 60f / 64f, 0f));
        }
    }
}
