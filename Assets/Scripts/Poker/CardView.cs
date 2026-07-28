using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>Where a zone wants a card to sit. Zones assign these; the card eases toward it.</summary>
    public struct CardPose
    {
        public Vector2 Position;
        public float Rotation;
        public int SortingOrder;

        public CardPose(Vector2 position, float rotation, int sortingOrder)
        {
            Position = position;
            Rotation = rotation;
            SortingOrder = sortingOrder;
        }
    }

    /// <summary>
    /// One card on screen. It never decides where it belongs — a <see cref="CardZone"/> hands it a
    /// <see cref="CardPose"/> and the card eases toward it, adding its own hover and selection lift.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class CardView : MonoBehaviour, ICardPointerTarget
    {
        /// <summary>A lifted card must draw over its neighbours, so it jumps far up the sorting range.</summary>
        public const int HoverSortingBoost = 500;

        [SerializeField] CardSpriteLibrary library;

        [Header("Motion")]
        [Tooltip("Higher eases into place faster. Frame-rate independent.")]
        [SerializeField] float moveSharpness = 14f;
        [SerializeField] float hoverLift = 0.16f;
        [SerializeField] float selectedLift = 0.34f;
        [SerializeField] float hoverScale = 1.08f;

        SpriteRenderer sr;
        BoxCollider2D box;
        CardPose pose;
        bool hovered;
        bool selected;
        bool faceUp = true;

        public Card Card { get; private set; }
        public CardZone Zone { get; internal set; }
        public bool FaceUp => faceUp;
        public bool Hovered => hovered;
        public bool Selected => selected;
        public CardPose Pose => pose;
        public int SortingOrder => sr != null ? sr.sortingOrder : 0;

        /// <summary>Pointer pressed and released on this card. Zones and the table decide what it means.</summary>
        public event System.Action<CardView> Clicked;
        public event System.Action<CardView> SelectionChanged;

        /// <summary>
        /// World-space footprint of the card, taken from the collider rather than the sprite: the
        /// art sits on a padded square canvas, so sprite bounds would overstate it.
        /// </summary>
        public Vector2 Size => box != null ? box.size : Vector2.one;

        void Awake()
        {
            CacheComponents();
            RefreshSprite();
        }

        void CacheComponents()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (box == null) box = GetComponent<BoxCollider2D>();
        }

        public void Bind(CardSpriteLibrary spriteLibrary, Card card, bool showFaceUp = true)
        {
            CacheComponents();
            library = spriteLibrary;
            Card = card;
            faceUp = showFaceUp;
            RefreshSprite();
            name = "Card_" + card;
        }

        public void SetFaceUp(bool value)
        {
            if (faceUp == value) return;
            faceUp = value;
            RefreshSprite();
        }

        public void Flip() => SetFaceUp(!faceUp);

        void RefreshSprite()
        {
            if (sr == null || library == null) return;

            // The collider is deliberately left alone. Every card in the pack shares one size, so
            // the prefab owns the hitbox — and fitting it to sprite bounds would include the
            // transparent padding around the art, letting a fanned card steal its neighbour's clicks.
            sr.sprite = faceUp ? library.Face(Card) : library.Back;
        }

        /// <summary>Sets the target pose. Pass instant to snap, e.g. when building a scene.</summary>
        public void SetPose(CardPose value, bool instant = false)
        {
            pose = value;
            if (!instant) return;

            CacheComponents();
            transform.localPosition = value.Position;
            transform.localRotation = Quaternion.Euler(0f, 0f, value.Rotation);
            transform.localScale = Vector3.one;
            if (sr != null) sr.sortingOrder = value.SortingOrder;
        }

        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            SelectionChanged?.Invoke(this);
        }

        public void ToggleSelected() => SetSelected(!selected);

        void LateUpdate()
        {
            float lift = selected ? selectedLift : hovered ? hoverLift : 0f;
            var goalPosition = new Vector3(pose.Position.x, pose.Position.y + lift, 0f);
            var goalRotation = Quaternion.Euler(0f, 0f, pose.Rotation);
            float goalScale = hovered || selected ? hoverScale : 1f;

            // Exponential decay: same feel at any frame rate.
            float t = 1f - Mathf.Exp(-moveSharpness * Time.deltaTime);

            transform.localPosition = Vector3.Lerp(transform.localPosition, goalPosition, t);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, goalRotation, t);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * goalScale, t);

            if (sr != null)
                sr.sortingOrder = pose.SortingOrder + (hovered || selected ? HoverSortingBoost : 0);
        }

        int ICardPointerTarget.PointerSortingOrder => SortingOrder;
        void ICardPointerTarget.OnPointerEnter() => hovered = true;
        void ICardPointerTarget.OnPointerExit() => hovered = false;
        void ICardPointerTarget.OnPointerClick() => Clicked?.Invoke(this);
    }
}
