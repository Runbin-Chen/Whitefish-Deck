using System.Collections;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// Runs a round of 钓红点: the opening deal, then capturing pairs off the table.
    ///
    /// Select a hand card and the table cards it can take light up; clicking one moves both to the
    /// capture pile. Turn order — placing a card when nothing matches, then drawing — is not here yet.
    ///
    /// Other players are not dealt either, so the draw pile currently holds their cards too; compare
    /// <see cref="DealSettings.DrawPileCards"/> against the live pile count to see the gap.
    /// </summary>
    public class PokerTable : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] CardSpriteLibrary library;
        [SerializeField] CardView cardPrefab;
        [SerializeField] DeckZone deckZone;
        [SerializeField] TableZone tableZone;
        [SerializeField] HandZone handZone;
        [Tooltip("Where captured pairs go — the pile on the left.")]
        [SerializeField] PileZone capturePile;

        [Header("Deal")]
        [SerializeField] DealSettings dealSettings = DealSettings.ForPlayers(2);

        [Tooltip("Seconds between cards while dealing. 0 deals the whole round in one frame.")]
        [Min(0f)]
        [SerializeField] float dealInterval = 0.07f;

        [SerializeField] bool dealOnStart = true;

        [Header("Shuffle")]
        [SerializeField] bool useRandomSeed = true;
        [Tooltip("Used when the random seed is off, so a deal can be reproduced while debugging.")]
        [SerializeField] int shuffleSeed = 1;

        Coroutine dealRoutine;

        public DeckZone DeckZone => deckZone;
        public TableZone TableZone => tableZone;
        public HandZone HandZone => handZone;
        public PileZone CapturePile => capturePile;

        public DealSettings DealSettings => dealSettings;
        public bool IsDealing { get; private set; }

        /// <summary>Raised once every card of the opening deal has been handed out.</summary>
        public event System.Action<PokerTable> RoundDealt;

        /// <summary>Raised when a pair is taken, with the hand card first and the table card second.</summary>
        public event System.Action<Card, Card> Captured;

        void OnEnable()
        {
            if (deckZone != null) deckZone.Clicked += OnDeckClicked;
        }

        void OnDisable()
        {
            if (deckZone != null) deckZone.Clicked -= OnDeckClicked;
        }

        void Start()
        {
            if (dealOnStart) NewRound();
        }

        // ---------------------------------------------------------------- deal entry points

        [ContextMenu("New Round")]
        public void NewRound() => NewRound(dealSettings);

        /// <summary>
        /// Deals a round with an explicit split. This is the hook for game modes, difficulty
        /// settings and tests — the player count is carried over from the current settings.
        /// </summary>
        public void NewRound(int handCards, int tableCards) =>
            NewRound(new DealSettings(dealSettings.PlayerCount, handCards, tableCards));

        public void NewRound(DealSettings settings)
        {
            if (!Validate()) return;

            if (!settings.IsValid)
            {
                Debug.LogError($"[PokerTable] {settings} needs more than {Card.DeckSize} cards.", this);
                return;
            }

            dealSettings = settings;

            if (dealRoutine != null)
            {
                StopCoroutine(dealRoutine);
                dealRoutine = null;
            }

            tableZone.Clear();
            handZone.Clear();
            if (capturePile != null) capturePile.Clear();
            deckZone.ResetAndShuffle(useRandomSeed ? (int?)null : shuffleSeed);

            // Coroutines need a running player loop, and a zero interval has nothing to wait for.
            if (Application.isPlaying && dealInterval > 0f)
                dealRoutine = StartCoroutine(DealRoutine(settings));
            else
                DealImmediate(settings);
        }

        [ContextMenu("Preset: 2 Players (10 / 12 / 20)")]
        void Preset2P() => NewRound(DealSettings.ForPlayers(2));

        [ContextMenu("Preset: 3 Players (7 / 10 / 21)")]
        void Preset3P() => NewRound(DealSettings.ForPlayers(3));

        [ContextMenu("Preset: 4 Players (5 / 12 / 20)")]
        void Preset4P() => NewRound(DealSettings.ForPlayers(4));

        // ---------------------------------------------------------------- dealing

        IEnumerator DealRoutine(DealSettings settings)
        {
            IsDealing = true;
            var beat = new WaitForSeconds(dealInterval);

            // Rule order: hand first, then the table spread, then the rest stays face down.
            for (int i = 0; i < settings.HandCardsPerPlayer; i++)
            {
                if (!DealOne(handZone, faceUp: true)) break;
                yield return beat;
            }

            for (int i = 0; i < settings.TableCards; i++)
            {
                if (!DealOne(tableZone, faceUp: true)) break;
                yield return beat;
            }

            IsDealing = false;
            dealRoutine = null;
            FinishDeal(settings);
        }

        void DealImmediate(DealSettings settings)
        {
            for (int i = 0; i < settings.HandCardsPerPlayer; i++)
                if (!DealOne(handZone, faceUp: true)) break;

            for (int i = 0; i < settings.TableCards; i++)
                if (!DealOne(tableZone, faceUp: true)) break;

            FinishDeal(settings);
        }

        /// <summary>Moves one card from the pile into a zone. False once the pile is empty.</summary>
        public bool DealOne(CardZone zone, bool faceUp)
        {
            if (zone == null || !deckZone.TryDraw(out Card card)) return false;

            zone.Add(SpawnCard(card, faceUp));
            return true;
        }

        void FinishDeal(DealSettings settings)
        {
            Debug.Log($"[PokerTable] {settings} | dealt hand {handZone.Count}, table {tableZone.Count}, " +
                      $"pile {deckZone.Count} (other players not dealt yet)", this);
            RoundDealt?.Invoke(this);
        }

        CardView SpawnCard(Card card, bool faceUp)
        {
            CardView view = Instantiate(cardPrefab);
            view.Bind(library, card, faceUp);

            // Start on the pile so the card visibly travels to its slot.
            view.transform.position = deckZone.TopWorldPosition;
            view.Clicked += OnCardClicked;
            return view;
        }

        // ---------------------------------------------------------------- interaction

        void OnCardClicked(CardView view)
        {
            if (IsDealing) return;

            if (view.Zone == handZone)
            {
                handZone.ToggleSelection(view);
                RefreshCaptureHints();
            }
            else if (view.Zone == tableZone)
            {
                TryCapture(view);
            }
        }

        /// <summary>
        /// Takes the selected hand card together with a table card, if the two capture.
        /// Both end up on the capture pile. False when nothing is selected or the pair is illegal.
        /// </summary>
        public bool TryCapture(CardView tableCard)
        {
            if (capturePile == null || tableCard == null || tableCard.Zone != tableZone) return false;

            var selected = handZone.GetSelected();
            if (selected.Count != 1) return false;

            CardView handCard = selected[0];
            if (!CaptureRules.CanCapture(handCard.Card, tableCard.Card)) return false;

            // Hand card first: it is the one that was played.
            capturePile.Add(handCard);
            capturePile.Add(tableCard);

            RefreshCaptureHints();
            Debug.Log($"[PokerTable] 钓走 {CaptureRules.Explain(handCard.Card, tableCard.Card)} " +
                      $"— 收牌堆 {capturePile.Count} 张", this);
            Captured?.Invoke(handCard.Card, tableCard.Card);
            return true;
        }

        /// <summary>Tints every table card the selected hand card could take.</summary>
        void RefreshCaptureHints()
        {
            var selected = handZone.GetSelected();
            bool hasPick = selected.Count == 1;
            Card pick = hasPick ? selected[0].Card : default;

            foreach (CardView card in tableZone.Cards)
            {
                if (card == null) continue;
                card.SetHighlighted(hasPick && CaptureRules.CanCapture(pick, card.Card));
            }
        }

        void OnDeckClicked(DeckZone deck)
        {
            if (IsDealing) return;
            DealOne(handZone, faceUp: true);
        }

        bool Validate()
        {
            if (library != null && cardPrefab != null && deckZone != null &&
                tableZone != null && handZone != null && capturePile != null)
                return true;

            Debug.LogError("[PokerTable] Missing references — assign the library, card prefab, " +
                           "the three zones and the capture pile.", this);
            return false;
        }
    }
}
