using System.Collections.Generic;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// A place on the table that owns card views and decides where each one sits.
    /// Subclasses only implement <see cref="Relayout"/>; membership is handled here.
    /// </summary>
    public abstract class CardZone : MonoBehaviour
    {
        [Tooltip("Cards in this zone sort from here upward, so zones never fight over draw order.")]
        [SerializeField] protected int baseSortingOrder;

        protected readonly List<CardView> cards = new List<CardView>();

        public IReadOnlyList<CardView> Cards => cards;
        public int Count => cards.Count;

        /// <summary>Raised after a card joins or leaves.</summary>
        public event System.Action<CardZone> ContentsChanged;

        public virtual void Add(CardView card, bool instant = false)
        {
            if (card == null || cards.Contains(card)) return;

            CardZone previous = card.Zone;
            if (previous != null && previous != this)
                previous.Remove(card);

            cards.Add(card);
            card.Zone = this;

            // Keep the world pose so the card visibly travels from wherever it was.
            card.transform.SetParent(transform, true);

            Relayout(instant);
            ContentsChanged?.Invoke(this);
        }

        public virtual bool Remove(CardView card)
        {
            if (card == null || !cards.Remove(card)) return false;

            if (card.Zone == this)
                card.Zone = null;

            Relayout();
            ContentsChanged?.Invoke(this);
            return true;
        }

        public virtual void Clear(bool destroyCards = true)
        {
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                CardView card = cards[i];
                if (card == null) continue;

                card.Zone = null;
                if (!destroyCards) continue;

                if (Application.isPlaying)
                    Destroy(card.gameObject);
                else
                    DestroyImmediate(card.gameObject);
            }

            cards.Clear();
            ContentsChanged?.Invoke(this);
        }

        /// <summary>Assigns a pose to every card. Called whenever membership changes.</summary>
        public abstract void Relayout(bool instant = false);

        protected virtual void OnValidate()
        {
            if (Application.isPlaying)
                Relayout();
        }
    }
}
