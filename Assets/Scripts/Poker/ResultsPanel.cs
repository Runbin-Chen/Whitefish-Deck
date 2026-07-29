using System.Text;
using TMPro;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// The end-of-round scoreboard. Hidden until the pile and the hands run out, then it dims the
    /// table and shows what each seat took.
    ///
    /// The text is English on purpose: the Kenney font that came with the UI pack only carries
    /// Latin glyphs, so Chinese would render as blanks until a CJK font asset is added.
    /// </summary>
    public class ResultsPanel : MonoBehaviour
    {
        [SerializeField] PokerTable table;

        [Header("Pieces")]
        [Tooltip("Full-bleed sprite that dims the table behind the panel.")]
        [SerializeField] SpriteRenderer dimmer;
        [SerializeField] SpriteRenderer background;
        [SerializeField] TMP_Text label;
        [SerializeField] TableButton newRoundButton;

        [Header("Sorting")]
        [Tooltip("Above every card and button on the felt.")]
        [SerializeField] int sortingOrder = 1000;

        public bool IsShown { get; private set; }

        void Awake()
        {
            ApplySorting();
            SetVisible(false);
        }

        void OnEnable()
        {
            if (table != null)
            {
                table.RoundFinished += OnRoundFinished;
                table.RoundStarted += OnRoundStarted;
            }
            if (newRoundButton != null) newRoundButton.Clicked += OnNewRoundClicked;
        }

        void OnDisable()
        {
            if (table != null)
            {
                table.RoundFinished -= OnRoundFinished;
                table.RoundStarted -= OnRoundStarted;
            }
            if (newRoundButton != null) newRoundButton.Clicked -= OnNewRoundClicked;
        }

        void OnRoundFinished(PokerTable finished) => Show();

        /// <summary>
        /// Clear on round *start*, not on the deal finishing — otherwise a round begun in code
        /// rather than through the button leaves this board hanging over the whole deal.
        /// </summary>
        void OnRoundStarted(PokerTable started) => SetVisible(false);

        void OnNewRoundClicked(TableButton button)
        {
            if (table != null) table.NewRound();
        }

        [ContextMenu("Show")]
        public void Show()
        {
            if (label != null) label.text = BuildSummary();
            SetVisible(true);
        }

        string BuildSummary()
        {
            var text = new StringBuilder("ROUND OVER\n\n");
            if (table == null) return text.ToString();

            int best = int.MinValue;
            int winners = 0;
            PlayerSeat leader = null;

            foreach (PlayerSeat seat in table.Seats)
            {
                if (seat == null) continue;
                text.Append(seat.DisplayName.ToUpperInvariant())
                    .Append("  ")
                    .Append(seat.Score)
                    .Append("\n");

                if (seat.Score > best) { best = seat.Score; leader = seat; winners = 1; }
                else if (seat.Score == best) winners++;
            }

            text.Append('\n');
            if (winners > 1) text.Append("DRAW");
            else if (leader != null) text.Append(leader.DisplayName.ToUpperInvariant()).Append(" WINS");

            return text.ToString();
        }

        void SetVisible(bool value)
        {
            IsShown = value;

            if (dimmer != null) dimmer.enabled = value;
            if (background != null) background.enabled = value;
            if (label != null) label.enabled = value;
            if (newRoundButton != null) newRoundButton.Show(value);
        }

        void ApplySorting()
        {
            if (dimmer != null) dimmer.sortingOrder = sortingOrder;
            if (background != null) background.sortingOrder = sortingOrder + 1;

            var meshRenderer = label != null ? label.GetComponent<MeshRenderer>() : null;
            if (meshRenderer != null) meshRenderer.sortingOrder = sortingOrder + 2;
        }
    }
}
