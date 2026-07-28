using System.Collections;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>Where a turn currently is. Clicks mean different things in each phase.</summary>
    public enum TurnPhase
    {
        Idle,        // dealing, or between rounds
        Play,        // waiting for the player to capture with, or put down, a hand card
        ResolveDraw, // a card has been drawn and is waiting to be resolved
        Finished,    // the round is over
    }

    /// <summary>
    /// Runs a round of 钓红点.
    ///
    /// Capturing never costs you the turn: keep taking pairs off the table for as long as anything
    /// in hand pairs. Only when nothing pairs do you lay a card down — and *that* is what triggers
    /// the draw, which in turn either captures or stays on the table.
    ///
    /// (The table game passes to the next player after a single capture. Until seats exist, one
    /// player keeps going until they run dry, which is the same sequence with the hand-off removed.)
    ///
    /// Two things are deliberate. Capturing is *mandatory*: laying a card down is only offered when
    /// nothing in hand can take anything, so a card can never be thrown away while a capture is on
    /// offer. And nothing resolves itself — the drawn card waits in <see cref="drawSlot"/> until the
    /// player clicks a target or the continue button, even when only one capture is legal.
    ///
    /// Only one seat is dealt so far, so the pile still holds the other players' cards and the
    /// hand empties first — that is what ends the round for now.
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
        [Tooltip("Holds the freshly drawn card, above the deck, until it is resolved.")]
        [SerializeField] SlotZone drawSlot;
        [Tooltip("Above the drawn card: appears when that card cannot capture anything.")]
        [SerializeField] TableButton continueButton;
        [Tooltip("Above the hand: appears when nothing in hand can capture, so a card must go down.")]
        [SerializeField] TableButton placeButton;

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
        public SlotZone DrawSlot => drawSlot;
        public TableButton ContinueButton => continueButton;
        public TableButton PlaceButton => placeButton;

        public DealSettings DealSettings => dealSettings;
        public bool IsDealing { get; private set; }
        public TurnPhase Phase { get; private set; } = TurnPhase.Idle;

        /// <summary>The card waiting in the draw slot, if any.</summary>
        public CardView DrawnCard => drawSlot != null ? drawSlot.Top : null;

        public event System.Action<PokerTable> RoundDealt;
        /// <summary>A pair was taken: the played card first, the table card it took second.</summary>
        public event System.Action<Card, Card> Captured;
        public event System.Action<PokerTable> TurnEnded;
        public event System.Action<PokerTable> RoundFinished;

        void OnEnable()
        {
            if (tableZone != null) tableZone.EmptyAreaClicked += OnTableEmptyClicked;
            if (continueButton != null) continueButton.Clicked += OnContinueClicked;
            if (placeButton != null) placeButton.Clicked += OnPlaceClicked;
        }

        void OnDisable()
        {
            if (tableZone != null) tableZone.EmptyAreaClicked -= OnTableEmptyClicked;
            if (continueButton != null) continueButton.Clicked -= OnContinueClicked;
            if (placeButton != null) placeButton.Clicked -= OnPlaceClicked;
        }

        void Start()
        {
            if (dealOnStart) NewRound();
        }

        // ---------------------------------------------------------------- deal

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
            Phase = TurnPhase.Idle;

            if (dealRoutine != null)
            {
                StopCoroutine(dealRoutine);
                dealRoutine = null;
            }

            HideButtons();
            drawSlot.Clear();
            tableZone.Clear();
            handZone.Clear();
            capturePile.Clear();
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

            Phase = TurnPhase.Play;
            Refresh();
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

        // ---------------------------------------------------------------- the turn

        void OnCardClicked(CardView view)
        {
            if (IsDealing || view == null) return;

            switch (Phase)
            {
                case TurnPhase.Play:
                    if (view.Zone == handZone)
                    {
                        handZone.ToggleSelection(view);
                        Refresh();
                    }
                    else if (view.Zone == tableZone && TryCapture(view))
                    {
                        // Capturing does not cost the turn — keep taking while anything pairs.
                        ContinuePlay();
                    }
                    break;

                case TurnPhase.ResolveDraw:
                    // Only the drawn card acts now; the hand is out of play until it is resolved.
                    if (view.Zone == tableZone)
                        ResolveDrawAgainst(view);
                    break;
            }
        }

        /// <summary>
        /// Bare felt only clears the selection. It deliberately does *not* play the card: the drop
        /// area covers the gaps between table cards, so a click that just misses a card used to
        /// throw the turn away with no confirmation.
        /// </summary>
        void OnTableEmptyClicked(TableZone zone)
        {
            if (IsDealing || Phase != TurnPhase.Play) return;

            handZone.ClearSelection();
            Refresh();
        }

        /// <summary>
        /// Takes the selected hand card together with a table card, if the two capture.
        /// Both end up on the capture pile. False when nothing is selected or the pair is illegal.
        /// </summary>
        public bool TryCapture(CardView tableCard)
        {
            if (tableCard == null || tableCard.Zone != tableZone) return false;

            var selected = handZone.GetSelected();
            if (selected.Count != 1) return false;

            CardView handCard = selected[0];
            if (!CaptureRules.CanCapture(handCard.Card, tableCard.Card)) return false;

            Capture(handCard, tableCard);
            return true;
        }

        /// <summary>Rule ②: lay a hand card face up on the table.</summary>
        public bool PlaceOnTable(CardView handCard)
        {
            if (handCard == null || handCard.Zone != handZone) return false;

            handCard.SetSelected(false);
            handCard.SetFaceUp(true);
            tableZone.Add(handCard);
            Debug.Log($"[PokerTable] 打出 {handCard.Card} 到台面", this);
            return true;
        }

        /// <summary>The player confirmed laying the selected card down. Only offered when nothing
        /// in hand could capture, so this can never throw away a legal take.</summary>
        void OnPlaceClicked(TableButton button)
        {
            if (Phase != TurnPhase.Play || HandHasAnyCapture()) return;

            var selected = handZone.GetSelected();
            if (selected.Count != 1) return;

            PlaceOnTable(selected[0]);
            BeginDraw();
        }

        /// <summary>
        /// Rule ③: draw one card. It waits in the slot above the pile — never resolving itself,
        /// even when only one capture is legal.
        /// </summary>
        void BeginDraw()
        {
            handZone.ClearSelection();
            HideButtons();
            ClearCaptureHints();

            if (deckZone.IsEmpty)
            {
                Debug.Log("[PokerTable] 牌堆已空,跳过摸牌", this);
                EndTurn();
                return;
            }

            deckZone.TryDraw(out Card card);
            CardView drawn = SpawnCard(card, faceUp: true);
            drawSlot.Add(drawn);

            // Lift it so it is obvious the game is waiting on this card, not on the hand.
            drawn.SetSelected(true);
            Phase = TurnPhase.ResolveDraw;

            int targets = CountTargets(card);
            Debug.Log(targets > 0
                ? $"[PokerTable] 摸到 {card},{targets} 个可钓目标,请点选"
                : $"[PokerTable] 摸到 {card},无法钓 — 请点「继续」", this);

            Refresh();
        }

        void ResolveDrawAgainst(CardView target)
        {
            CardView drawn = DrawnCard;
            if (drawn == null || target == null) return;
            if (!CaptureRules.CanCapture(drawn.Card, target.Card)) return;

            drawn.SetSelected(false);
            Capture(drawn, target);
            EndTurn();
        }

        /// <summary>
        /// Stay in the play phase after a capture, or close the round out if that was the last
        /// card in hand.
        /// </summary>
        void ContinuePlay()
        {
            handZone.ClearSelection();
            ClearCaptureHints();

            if (RoundIsOver()) return;

            Phase = TurnPhase.Play;
            Refresh();
        }

        bool RoundIsOver()
        {
            // The rules end the round when hands and the draw pile are both spent. With one seat
            // dealt they do not empty together, so the empty hand is what stops play.
            if (handZone.Count > 0) return false;

            Phase = TurnPhase.Finished;
            HideButtons();
            Debug.Log($"[PokerTable] 本局结束 — 收牌堆 {capturePile.Count} 张," +
                      $"牌堆剩 {deckZone.Count},台面 {tableZone.Count}", this);
            RoundFinished?.Invoke(this);
            return true;
        }

        /// <summary>The drawn card could not capture; the player has acknowledged it.</summary>
        void OnContinueClicked(TableButton button)
        {
            if (Phase != TurnPhase.ResolveDraw) return;

            CardView drawn = DrawnCard;
            if (drawn != null)
            {
                drawn.SetSelected(false);
                tableZone.Add(drawn);
                Debug.Log($"[PokerTable] {drawn.Card} 留在台面", this);
            }

            EndTurn();
        }

        void Capture(CardView played, CardView tableCard)
        {
            capturePile.Add(played);
            capturePile.Add(tableCard);

            Debug.Log($"[PokerTable] 钓走 {CaptureRules.Explain(played.Card, tableCard.Card)} " +
                      $"— 收牌堆 {capturePile.Count} 张", this);
            Captured?.Invoke(played.Card, tableCard.Card);
        }

        /// <summary>The drawn card has been dealt with, so the turn proper is over.</summary>
        void EndTurn()
        {
            handZone.ClearSelection();
            HideButtons();
            ClearCaptureHints();

            if (RoundIsOver()) return;

            Phase = TurnPhase.Play;
            Refresh();
            TurnEnded?.Invoke(this);
        }

        // ---------------------------------------------------------------- hints and buttons

        int CountTargets(Card probe)
        {
            int count = 0;
            foreach (CardView c in tableZone.Cards)
                if (c != null && CaptureRules.CanCapture(probe, c.Card)) count++;
            return count;
        }

        /// <summary>Rule ①: if anything in hand can take something, a capture is compulsory.</summary>
        public bool HandHasAnyCapture()
        {
            foreach (CardView h in handZone.Cards)
            {
                if (h == null) continue;
                if (CountTargets(h.Card) > 0) return true;
            }
            return false;
        }

        void Refresh()
        {
            RefreshCaptureHints();
            RefreshButtons();
        }

        /// <summary>Tints every table card the current card could take.</summary>
        void RefreshCaptureHints()
        {
            bool hasProbe = false;
            Card probe = default;

            if (Phase == TurnPhase.Play)
            {
                var selected = handZone.GetSelected();
                if (selected.Count == 1) { probe = selected[0].Card; hasProbe = true; }
            }
            else if (Phase == TurnPhase.ResolveDraw && DrawnCard != null)
            {
                probe = DrawnCard.Card;
                hasProbe = true;
            }

            foreach (CardView card in tableZone.Cards)
            {
                if (card == null) continue;
                card.SetHighlighted(hasProbe && CaptureRules.CanCapture(probe, card.Card));
            }
        }

        void RefreshButtons()
        {
            if (Phase == TurnPhase.Play)
            {
                continueButton.Show(false);
                // Laying a card down is only legal — and only offered — when nothing can be taken.
                placeButton.Show(!HandHasAnyCapture() && handZone.GetSelected().Count == 1);
            }
            else if (Phase == TurnPhase.ResolveDraw)
            {
                placeButton.Show(false);
                continueButton.Show(DrawnCard != null && CountTargets(DrawnCard.Card) == 0);
            }
            else
            {
                HideButtons();
            }
        }

        void HideButtons()
        {
            continueButton.Show(false);
            placeButton.Show(false);
        }

        void ClearCaptureHints()
        {
            foreach (CardView card in tableZone.Cards)
                if (card != null) card.SetHighlighted(false);
        }

        bool Validate()
        {
            if (library != null && cardPrefab != null && deckZone != null && tableZone != null &&
                handZone != null && capturePile != null && drawSlot != null &&
                continueButton != null && placeButton != null)
                return true;

            Debug.LogError("[PokerTable] Missing references — assign the library, card prefab, " +
                           "the zones, the capture pile, the draw slot and both buttons.", this);
            return false;
        }
    }
}
