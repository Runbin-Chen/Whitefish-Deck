using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// A world-space button that lives on the felt, hidden until the game needs an answer.
    /// It routes through the same pointer path as the cards, so there is no second input system
    /// and no Canvas to keep in sync with the table layout.
    /// </summary>
    public class TableButton : MonoBehaviour, ICardPointerTarget
    {
        [SerializeField] SpriteRenderer background;
        [SerializeField] SpriteRenderer icon;
        [SerializeField] BoxCollider2D hitbox;

        [Header("Look")]
        [Tooltip("Well above the cards: when this is up, it is the only thing to click.")]
        [SerializeField] int sortingOrder = 400;
        [SerializeField] Color normalTint = new Color32(0x3A, 0x28, 0x18, 240);
        [SerializeField] Color hoverTint = new Color32(0x5A, 0x40, 0x24, 245);
        [SerializeField] float hoverScale = 1.06f;
        [SerializeField] float scaleSharpness = 16f;

        bool shown;
        bool hovered;

        public bool IsShown => shown;
        public event System.Action<TableButton> Clicked;

        void Awake()
        {
            CacheRenderers();
            Show(false);
        }

        void CacheRenderers()
        {
            if (background == null) background = GetComponent<SpriteRenderer>();
            if (hitbox == null) hitbox = GetComponent<BoxCollider2D>();
        }

        public void Show(bool value)
        {
            CacheRenderers();
            shown = value;
            hovered = false;

            if (background != null)
            {
                background.enabled = value;
                background.sortingOrder = sortingOrder;
                background.color = normalTint;
            }

            if (icon != null)
            {
                icon.enabled = value;
                icon.sortingOrder = sortingOrder + 1;
            }

            if (hitbox != null) hitbox.enabled = value;

            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (!shown) return;

            float t = 1f - Mathf.Exp(-scaleSharpness * Time.deltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * (hovered ? hoverScale : 1f), t);

            if (background != null)
                background.color = Color.Lerp(background.color, hovered ? hoverTint : normalTint, t);
        }

        int ICardPointerTarget.PointerSortingOrder => sortingOrder;
        void ICardPointerTarget.OnPointerEnter() => hovered = shown;
        void ICardPointerTarget.OnPointerExit() => hovered = false;

        void ICardPointerTarget.OnPointerClick()
        {
            if (shown) Clicked?.Invoke(this);
        }
    }
}
