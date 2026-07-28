using UnityEngine;
using UnityEngine.InputSystem;

namespace Whitefish.Poker
{
    /// <summary>
    /// Routes pointer hover and clicks to <see cref="ICardPointerTarget"/>s.
    ///
    /// This project is set to the Input System package only, so Unity never sends OnMouseEnter /
    /// OnMouseDown. We read the pointer ourselves and overlap-test the 2D colliders instead.
    /// Using Pointer rather than Mouse means touch works without a second code path.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CardPointerInput : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] LayerMask cardMask = ~0;

        static readonly Collider2D[] Hits = new Collider2D[24];

        ICardPointerTarget hovered;
        ICardPointerTarget pressed;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || cam == null) return;

            Vector2 screen = pointer.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            ICardPointerTarget top = TopTargetAt(world);

            if (!ReferenceEquals(top, hovered))
            {
                hovered?.OnPointerExit();
                hovered = top;
                hovered?.OnPointerEnter();
            }

            if (pointer.press.wasPressedThisFrame)
            {
                pressed = top;
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                // Only a press and release on the same target counts as a click.
                if (pressed != null && ReferenceEquals(pressed, top))
                    top.OnPointerClick();
                pressed = null;
            }
        }

        ICardPointerTarget TopTargetAt(Vector3 world)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = cardMask,
                useTriggers = true,
            };

            int count = Physics2D.OverlapPoint((Vector2)world, filter, Hits);

            ICardPointerTarget best = null;
            int bestOrder = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                var target = Hits[i].GetComponentInParent<ICardPointerTarget>();
                if (target == null) continue;

                if (target.PointerSortingOrder > bestOrder)
                {
                    bestOrder = target.PointerSortingOrder;
                    best = target;
                }
            }

            return best;
        }

        void OnDisable()
        {
            hovered?.OnPointerExit();
            hovered = null;
            pressed = null;
        }
    }
}
