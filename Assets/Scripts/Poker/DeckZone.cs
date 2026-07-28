using System.Collections.Generic;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// The draw pile. It holds card *data*, not card views — 52 GameObjects for a stack the player
    /// only sees the top of would be waste. The stack is faked with a handful of back sprites whose
    /// height tracks the remaining count.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class DeckZone : MonoBehaviour, ICardPointerTarget
    {
        [SerializeField] CardSpriteLibrary library;

        [Header("Stack look")]
        [Tooltip("Most back sprites drawn at once, however many cards remain.")]
        [SerializeField] int visibleStackMax = 7;
        [Tooltip("Cards represented by each drawn back sprite.")]
        [SerializeField] int cardsPerStackSprite = 8;
        [SerializeField] Vector2 stackOffset = new Vector2(0.012f, 0.018f);
        [SerializeField] int baseSortingOrder = -50;

        readonly List<SpriteRenderer> stack = new List<SpriteRenderer>();
        Deck deck = new Deck();
        BoxCollider2D box;
        int visibleCount;

        public Deck Deck => deck;
        public int Count => deck.Count;
        public bool IsEmpty => deck.IsEmpty;

        public event System.Action<int> CountChanged;
        /// <summary>Pointer clicked the pile. The table decides whether that deals a card.</summary>
        public event System.Action<DeckZone> Clicked;

        /// <summary>Where a newly drawn card should start its flight from.</summary>
        public Vector3 TopWorldPosition
        {
            get
            {
                int top = Mathf.Max(0, visibleCount - 1);
                return transform.TransformPoint(new Vector3(stackOffset.x * top, stackOffset.y * top, 0f));
            }
        }

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            RefreshVisual();
        }

        /// <summary>Refills with a standard 52 and shuffles. Pass a seed for a reproducible deal.</summary>
        public void ResetAndShuffle(int? seed = null)
        {
            deck = new Deck(seed);
            deck.FillStandard52();
            deck.Shuffle();
            RefreshVisual();
        }

        public bool TryDraw(out Card card)
        {
            bool drawn = deck.TryDraw(out card);
            if (drawn) RefreshVisual();
            return drawn;
        }

        void RefreshVisual()
        {
            EnsureStackSprites(visibleStackMax);

            visibleCount = deck.Count == 0
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(deck.Count / (float)Mathf.Max(1, cardsPerStackSprite)), 1, visibleStackMax);

            for (int i = 0; i < stack.Count; i++)
            {
                bool visible = i < visibleCount;
                stack[i].gameObject.SetActive(visible);
                if (!visible) continue;

                stack[i].sprite = library != null ? library.Back : null;
                stack[i].transform.localPosition = new Vector3(stackOffset.x * i, stackOffset.y * i, 0f);
                stack[i].sortingOrder = baseSortingOrder + i;
            }

            // Size is set on the object itself (the art has transparent padding, so sprite bounds
            // would overstate it); we only decide whether an empty pile can still be clicked.
            if (box != null)
                box.enabled = visibleCount > 0;

            CountChanged?.Invoke(deck.Count);
        }

        void EnsureStackSprites(int count)
        {
            while (stack.Count < count)
            {
                var go = new GameObject($"Back_{stack.Count}");
                go.transform.SetParent(transform, false);
                stack.Add(go.AddComponent<SpriteRenderer>());
            }
        }

        int ICardPointerTarget.PointerSortingOrder => baseSortingOrder + visibleCount;
        void ICardPointerTarget.OnPointerEnter() { }
        void ICardPointerTarget.OnPointerExit() { }
        void ICardPointerTarget.OnPointerClick() => Clicked?.Invoke(this);
    }
}
