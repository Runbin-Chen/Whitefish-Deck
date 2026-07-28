using System.Collections.Generic;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// The player's hand: a shallow fan along the bottom of the screen. Cards keep an even
    /// spacing until they would overflow <see cref="maxWidth"/>, then they tighten instead of
    /// running off screen.
    /// </summary>
    public class HandZone : CardZone
    {
        [Header("Fan")]
        [Tooltip("Ideal gap between card centres, in world units.")]
        [SerializeField] float spacing = 0.62f;
        [Tooltip("The fan tightens rather than growing past this width.")]
        [SerializeField] float maxWidth = 5.4f;
        [Tooltip("How far the outer cards dip below the middle one.")]
        [SerializeField] float arcDepth = 0.18f;
        [Tooltip("Tilt of the outermost cards, in degrees.")]
        [SerializeField] float maxTilt = 8f;

        [Header("Selection")]
        [Tooltip("How many cards can be lifted at once. 0 means no limit.")]
        [SerializeField] int maxSelected = 5;

        public int MaxSelected => maxSelected;

        public int SelectedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null && cards[i].Selected) count++;
                return count;
            }
        }

        public override void Relayout(bool instant = false)
        {
            int n = cards.Count;
            if (n == 0) return;

            float step = spacing;
            if (n > 1 && step * (n - 1) > maxWidth)
                step = maxWidth / (n - 1);

            float startX = -step * (n - 1) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                if (cards[i] == null) continue;

                // t runs -1 (left edge) .. 0 (middle) .. 1 (right edge).
                float t = n == 1 ? 0f : i / (float)(n - 1) * 2f - 1f;

                float x = startX + step * i;
                float y = -arcDepth * t * t;
                float rotation = -maxTilt * t;

                cards[i].SetPose(new CardPose(new Vector2(x, y), rotation, baseSortingOrder + i), instant);
            }
        }

        /// <summary>Selects or deselects a card. Returns false when the selection cap blocks it.</summary>
        public bool ToggleSelection(CardView card)
        {
            if (card == null || !cards.Contains(card)) return false;

            if (card.Selected)
            {
                card.SetSelected(false);
                return true;
            }

            if (maxSelected > 0 && SelectedCount >= maxSelected)
                return false;

            card.SetSelected(true);
            return true;
        }

        public List<CardView> GetSelected()
        {
            var selected = new List<CardView>();
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null && cards[i].Selected) selected.Add(cards[i]);
            return selected;
        }

        /// <summary>Removes the selected cards from the hand and returns them, still alive.</summary>
        public List<CardView> TakeSelected()
        {
            List<CardView> selected = GetSelected();
            for (int i = 0; i < selected.Count; i++)
            {
                selected[i].SetSelected(false);
                Remove(selected[i]);
            }
            return selected;
        }

        public void ClearSelection()
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) cards[i].SetSelected(false);
        }
    }
}
