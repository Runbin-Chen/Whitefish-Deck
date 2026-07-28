using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// Cards dealt out across the felt. They sit on a grid so every card stays reachable, then get
    /// a per-slot nudge and tilt so the spread reads as tossed down rather than filed away.
    /// The jitter is derived from the slot index, so a card does not twitch when a neighbour leaves.
    /// </summary>
    public class TableZone : CardZone, ICardPointerTarget
    {
        [Header("Area (local space)")]
        [SerializeField] Vector2 areaSize = new Vector2(6.6f, 2.4f);
        [Tooltip("Cards wrap to a new row past this many columns.")]
        [SerializeField] int maxColumns = 8;

        [Header("Placing")]
        [Tooltip("Collider covering the area. Clicking bare felt inside it plays the selected card.")]
        [SerializeField] BoxCollider2D dropArea;

        [Header("Scatter")]
        [SerializeField] float positionJitter = 0.12f;
        [SerializeField] float rotationJitter = 7f;
        [Tooltip("Change to reroll the scatter pattern.")]
        [SerializeField] int scatterSeed = 20260728;

        public override void Relayout(bool instant = false)
        {
            int n = cards.Count;
            if (n == 0) return;

            int columns = Mathf.Max(1, Mathf.Min(maxColumns, n));
            int rows = Mathf.CeilToInt(n / (float)columns);
            float cellWidth = areaSize.x / columns;
            float cellHeight = areaSize.y / rows;

            for (int i = 0; i < n; i++)
            {
                if (cards[i] == null) continue;

                int row = i / columns;
                int column = i % columns;

                // Centre short rows (the last one) instead of left-aligning them.
                int cardsInRow = Mathf.Min(columns, n - row * columns);
                float rowWidth = cardsInRow * cellWidth;

                float x = -rowWidth * 0.5f + cellWidth * (column + 0.5f);
                float y = areaSize.y * 0.5f - cellHeight * (row + 0.5f);

                x += Jitter(i, 0) * positionJitter;
                y += Jitter(i, 1) * positionJitter;
                float rotation = Jitter(i, 2) * rotationJitter;

                cards[i].SetPose(new CardPose(new Vector2(x, y), rotation, baseSortingOrder + i), instant);
            }
        }

        /// <summary>Stable pseudo-random in -1..1 for a slot, so the scatter never reshuffles itself.</summary>
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

        /// <summary>Raised when the pointer clicks bare felt rather than a card.</summary>
        public event System.Action<TableZone> EmptyAreaClicked;

        void Awake() => SyncDropArea();

        protected override void OnValidate()
        {
            base.OnValidate();
            SyncDropArea();
        }

        void SyncDropArea()
        {
            if (dropArea == null) dropArea = GetComponent<BoxCollider2D>();
            if (dropArea == null) return;

            dropArea.size = areaSize;
            dropArea.offset = Vector2.zero;
            dropArea.isTrigger = true;
        }

        // Sits one below the cards it holds, so a click only reaches the felt when it misses
        // every card sitting on it.
        int ICardPointerTarget.PointerSortingOrder => baseSortingOrder - 1;
        void ICardPointerTarget.OnPointerEnter() { }
        void ICardPointerTarget.OnPointerExit() { }
        void ICardPointerTarget.OnPointerClick() => EmptyAreaClicked?.Invoke(this);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, areaSize.y, 0f));
        }
    }
}
