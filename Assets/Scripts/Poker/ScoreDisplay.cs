using TMPro;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// Shows the running score for a capture pile, sitting just under it.
    ///
    /// Only red cards score, so the number happily sits still while black pairs pile up — the
    /// red-card count is available as {1} in <see cref="format"/> to explain why.
    /// </summary>
    public class ScoreDisplay : MonoBehaviour
    {
        [Tooltip("The pile whose captured cards are counted.")]
        [SerializeField] PileZone pile;

        [SerializeField] TMP_Text label;

        [Tooltip("{0} is the score, {1} the number of red cards taken.")]
        [SerializeField] string format = "{0}";

        public int Score { get; private set; }
        public int RedCards { get; private set; }

        void OnEnable()
        {
            if (pile != null) pile.ContentsChanged += OnPileChanged;
            Refresh();
        }

        void OnDisable()
        {
            if (pile != null) pile.ContentsChanged -= OnPileChanged;
        }

        void OnPileChanged(CardZone zone) => Refresh();

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            Score = pile != null ? ScoreRules.Total(pile.Cards) : 0;
            RedCards = pile != null ? ScoreRules.CountScoring(pile.Cards) : 0;

            if (label != null)
                label.text = string.Format(format, Score, RedCards);
        }
    }
}
