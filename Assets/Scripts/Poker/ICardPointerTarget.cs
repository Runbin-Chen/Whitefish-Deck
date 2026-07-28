namespace Whitefish.Poker
{
    /// <summary>
    /// Anything on the felt that reacts to the pointer. <see cref="CardPointerInput"/> raycasts
    /// 2D colliders and forwards events to the top-most target it finds.
    /// </summary>
    public interface ICardPointerTarget
    {
        /// <summary>Used to break ties when several targets overlap; highest wins.</summary>
        int PointerSortingOrder { get; }

        void OnPointerEnter();
        void OnPointerExit();
        void OnPointerClick();
    }
}
