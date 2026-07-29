using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>Where a turn currently is. Clicks mean different things in each phase.</summary>
    public enum TurnPhase
    {
        Idle,        // dealing, or between rounds
        Play,        // waiting for the seat to capture with, or put down, a hand card
        ResolveDraw, // a card has been drawn and is waiting to be resolved
        Finished,    // the round is over
    }

    /// <summary>
    /// Runs a round of 钓红点.
    ///
    /// Every turn is the same three beats: play one hand card — capturing with it if anything on
    /// the table pairs, otherwise laying it down — then draw one from the pile, which likewise
    /// either captures or stays on the table, then pass to the next seat.
    ///
    /// That one-card-per-turn shape is what makes the deal counts work: each turn spends exactly
    /// one hand card and one pile card, so 2P 10/10/20 has hands and pile running out together.
    ///
    /// Two things are deliberate. Capturing is *mandatory*: laying a card down is only offered when
    /// nothing in hand can take anything, so a card can never be thrown away while a capture is on
    /// offer. And for a human seat nothing resolves itself — the drawn card waits in
    /// <see cref="drawSlot"/> until they click a target or the continue button, even when only one
    /// capture is legal. Seats that are not <see cref="PlayerSeat.IsLocal"/> play themselves.
    /// </summary>
    public class PokerTable : MonoBehaviour
    {
        [Header("Seats")]
        [Tooltip("Turn order. The table deals to each in turn and passes play around them.")]
        [SerializeField] List<PlayerSeat> seats = new List<PlayerSeat>();

        [Header("Shared zones")]
        [SerializeField] CardSpriteLibrary library;
        [SerializeField] CardView cardPrefab;
        [SerializeField] DeckZone deckZone;
        [SerializeField] TableZone tableZone;
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

        [Header("Opponents")]
        [Tooltip("Pause before a non-local seat decides, so you can read the board first.")]
        [Min(0f)]
        [SerializeField] float opponentThinkTime = 0.9f;

        [Tooltip("Pause after it acts, so the cards finish moving before anything else happens.")]
        [Min(0f)]
        [SerializeField] float opponentSettleTime = 0.45f;

        [Header("Shuffle")]
        [SerializeField] bool useRandomSeed = true;
        [Tooltip("Used when the random seed is off, so a deal can be reproduced while debugging.")]
        [SerializeField] int shuffleSeed = 1;

        Coroutine dealRoutine;
        Coroutine opponentRoutine;
        int seatIndex;

        public IReadOnlyList<PlayerSeat> Seats => seats;
        public PlayerSeat CurrentSeat => seats != null && seatIndex >= 0 && seatIndex < seats.Count
            ? seats[seatIndex]
            : null;

        /// <summary>The hand being played this turn.</summary>
        public HandZone ActiveHand => CurrentSeat != null ? CurrentSeat.Hand : null;
        public PileZone ActivePile => CurrentSeat != null ? CurrentSeat.CapturePile : null;

        public DeckZone DeckZone => deckZone;
        public TableZone TableZone => tableZone;
        public SlotZone DrawSlot => drawSlot;
        public TableButton ContinueButton => continueButton;
        public TableButton PlaceButton => placeButton;

        public DealSettings DealSettings => dealSettings;
        public bool IsDealing { get; private set; }
        public TurnPhase Phase { get; private set; } = TurnPhase.Idle;

        /// <summary>The card waiting in the draw slot, if any.</summary>
        public CardView DrawnCard => drawSlot != null ? drawSlot.Top : null;

        public event System.Action<PokerTable> RoundDealt;
        /// <summary>A pair was taken: the seat, the played card, then the table card it took.</summary>
        public event System.Action<PlayerSeat, Card, Card> Captured;
        public event System.Action<PlayerSeat> TurnStarted;
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

        public void NewRound(int handCards, int tableCards) =>
            NewRound(new DealSettings(seats.Count, handCards, tableCards));

        public void NewRound(DealSettings settings)
        {
            if (!Validate()) return;

            // The seat list is the truth about how many players there are.
            settings.PlayerCount = seats.Count;

            if (!settings.IsValid)
            {
                Debug.LogError($"[PokerTable] {settings} needs more than {Card.DeckSize} cards.", this);
                return;
            }

            dealSettings = settings;
            Phase = TurnPhase.Idle;
            seatIndex = 0;

            StopRoutines();
            HideButtons();
            drawSlot.Clear();
            tableZone.Clear();
            foreach (PlayerSeat seat in seats)
            {
                seat.Hand.Clear();
                seat.CapturePile.Clear();
            }
            deckZone.ResetAndShuffle(useRandomSeed ? (int?)null : shuffleSeed);

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

            // Rule order: hands first, one card at a time around the table, then the spread.
            for (int i = 0; i < settings.HandCardsPerPlayer; i++)
            {
                foreach (PlayerSeat seat in seats)
                {
                    if (!DealOne(seat.Hand, seat.HandIsVisible)) break;
                    yield return beat;
                }
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
                foreach (PlayerSeat seat in seats)
                    DealOne(seat.Hand, seat.HandIsVisible);

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
            var hands = new System.Text.StringBuilder();
            foreach (PlayerSeat seat in seats) hands.Append($"{seat.DisplayName} {seat.Hand.Count}  ");

            Debug.Log($"[PokerTable] {settings} | {hands}台面 {tableZone.Count},牌堆 {deckZone.Count}", this);

            RoundDealt?.Invoke(this);
            BeginTurn();
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

        // ---------------------------------------------------------------- turns

        void BeginTurn()
        {
            PlayerSeat seat = CurrentSeat;
            if (seat == null) return;

            Phase = TurnPhase.Play;
            Refresh();
            TurnStarted?.Invoke(seat);

            if (!seat.IsLocal)
                opponentRoutine = StartCoroutine(OpponentPlay(seat));
        }

        /// <summary>Rule ④: play passes on once the drawn card has been dealt with.</summary>
        void NextSeat()
        {
            HideButtons();
            ClearCaptureHints();
            if (ActiveHand != null) ActiveHand.ClearSelection();

            if (RoundIsOver()) return;

            // Seats are listed in turn order; the table runs anticlockwise around them.
            seatIndex = (seatIndex + 1) % seats.Count;
            BeginTurn();
        }

        bool RoundIsOver()
        {
            foreach (PlayerSeat seat in seats)
                if (seat.Hand.Count > 0) return false;

            Phase = TurnPhase.Finished;
            StopRoutines();
            HideButtons();

            var summary = new System.Text.StringBuilder();
            foreach (PlayerSeat seat in seats)
                summary.Append($"{seat.DisplayName} {seat.Score}分({seat.RedCards}红)  ");

            Debug.Log($"[PokerTable] 本局结束 — {summary}| 牌堆剩 {deckZone.Count},台面 {tableZone.Count}", this);
            RoundFinished?.Invoke(this);
            return true;
        }

        // ---------------------------------------------------------------- player input

        void OnCardClicked(CardView view)
        {
            if (IsDealing || view == null) return;

            PlayerSeat seat = CurrentSeat;
            if (seat == null || !seat.IsLocal) return; // an opponent is thinking

            switch (Phase)
            {
                case TurnPhase.Play:
                    if (view.Zone == seat.Hand)
                    {
                        seat.Hand.ToggleSelection(view);
                        Refresh();
                    }
                    else if (view.Zone == tableZone && TryCapture(view))
                    {
                        // The draw closes every turn, whether the card was taken or laid down.
                        BeginDraw();
                    }
                    break;

                case TurnPhase.ResolveDraw:
                    if (view.Zone == tableZone)
                        ResolveDrawAgainst(view);
                    break;
            }
        }

        /// <summary>
        /// Bare felt only clears the selection. It deliberately does *not* play the card: the drop
        /// area covers the gaps between table cards, so a click that just misses a card would
        /// otherwise throw the turn away with no confirmation.
        /// </summary>
        void OnTableEmptyClicked(TableZone zone)
        {
            if (IsDealing || Phase != TurnPhase.Play) return;
            if (CurrentSeat == null || !CurrentSeat.IsLocal) return;

            CurrentSeat.Hand.ClearSelection();
            Refresh();
        }

        void OnPlaceClicked(TableButton button)
        {
            if (Phase != TurnPhase.Play || CurrentSeat == null || !CurrentSeat.IsLocal) return;
            if (HandHasAnyCapture(CurrentSeat.Hand)) return;

            var selected = CurrentSeat.Hand.GetSelected();
            if (selected.Count != 1) return;

            PlaceOnTable(selected[0]);
            BeginDraw();
        }

        void OnContinueClicked(TableButton button)
        {
            if (Phase != TurnPhase.ResolveDraw) return;
            if (CurrentSeat == null || !CurrentSeat.IsLocal) return;

            LeaveDrawnOnTable();
            NextSeat();
        }

        // ---------------------------------------------------------------- moves

        /// <summary>
        /// Takes the selected hand card together with a table card, if the two capture.
        /// Both end up on the seat's capture pile.
        /// </summary>
        public bool TryCapture(CardView tableCard)
        {
            PlayerSeat seat = CurrentSeat;
            if (seat == null || tableCard == null || tableCard.Zone != tableZone) return false;

            var selected = seat.Hand.GetSelected();
            if (selected.Count != 1) return false;

            CardView handCard = selected[0];
            if (!CaptureRules.CanCapture(handCard.Card, tableCard.Card)) return false;

            Capture(seat, handCard, tableCard);
            return true;
        }

        /// <summary>Rule ②: lay a hand card face up on the table.</summary>
        public bool PlaceOnTable(CardView handCard)
        {
            PlayerSeat seat = CurrentSeat;
            if (seat == null || handCard == null || handCard.Zone != seat.Hand) return false;

            handCard.SetSelected(false);
            handCard.SetFaceUp(true);
            tableZone.Add(handCard);
            Debug.Log($"[PokerTable] {seat.DisplayName} 打出 {handCard.Card} 到台面", this);
            return true;
        }

        /// <summary>Rule ③: draw one card. It waits in the slot above the pile.</summary>
        void BeginDraw()
        {
            PlayerSeat seat = CurrentSeat;
            if (seat != null) seat.Hand.ClearSelection();
            HideButtons();
            ClearCaptureHints();

            if (deckZone.IsEmpty)
            {
                Debug.Log("[PokerTable] 牌堆已空,跳过摸牌", this);
                NextSeat();
                return;
            }

            deckZone.TryDraw(out Card card);
            CardView drawn = SpawnCard(card, faceUp: true);
            drawSlot.Add(drawn);
            drawn.SetSelected(true); // lifted, so it is obvious the game is waiting on this card

            Phase = TurnPhase.ResolveDraw;

            int targets = CountTargets(card);
            Debug.Log(targets > 0
                ? $"[PokerTable] {seat.DisplayName} 摸到 {card},{targets} 个可钓目标"
                : $"[PokerTable] {seat.DisplayName} 摸到 {card},无法钓", this);

            Refresh();

            if (seat != null && !seat.IsLocal)
                opponentRoutine = StartCoroutine(OpponentResolveDraw(seat));
        }

        /// <summary>Takes the drawn card with a table card, without ending the turn.</summary>
        bool ResolveDrawCapture(CardView target)
        {
            CardView drawn = DrawnCard;
            PlayerSeat seat = CurrentSeat;
            if (drawn == null || target == null || seat == null) return false;
            if (!CaptureRules.CanCapture(drawn.Card, target.Card)) return false;

            drawn.SetSelected(false);
            Capture(seat, drawn, target);
            return true;
        }

        void ResolveDrawAgainst(CardView target)
        {
            if (ResolveDrawCapture(target)) NextSeat();
        }

        void LeaveDrawnOnTable()
        {
            CardView drawn = DrawnCard;
            if (drawn == null) return;

            drawn.SetSelected(false);
            tableZone.Add(drawn);
            Debug.Log($"[PokerTable] {drawn.Card} 留在台面", this);
        }

        void Capture(PlayerSeat seat, CardView played, CardView tableCard)
        {
            seat.CapturePile.Add(played);
            seat.CapturePile.Add(tableCard);

            Debug.Log($"[PokerTable] {seat.DisplayName} 钓走 " +
                      $"{CaptureRules.Explain(played.Card, tableCard.Card)} — " +
                      $"收牌堆 {seat.CapturePile.Count} 张,{seat.Score} 分", this);
            Captured?.Invoke(seat, played.Card, tableCard.Card);
        }

        // ---------------------------------------------------------------- opponents

        IEnumerator OpponentPlay(PlayerSeat seat)
        {
            yield return new WaitForSeconds(opponentThinkTime);

            if (Phase != TurnPhase.Play || CurrentSeat != seat) { opponentRoutine = null; yield break; }

            CardView best = null, bestTarget = null;
            int bestValue = int.MinValue;

            // Prefer the take that scores most; red cards are the only ones worth anything.
            foreach (CardView h in seat.Hand.Cards)
            {
                foreach (CardView t in tableZone.Cards)
                {
                    if (!CaptureRules.CanCapture(h.Card, t.Card)) continue;

                    int value = ScoreRules.Value(h.Card) + ScoreRules.Value(t.Card);
                    if (value > bestValue) { bestValue = value; best = h; bestTarget = t; }
                }
            }

            if (best != null)
            {
                seat.Hand.ClearSelection();
                seat.Hand.ToggleSelection(best);
                TryCapture(bestTarget);
            }
            else
            {
                // Nothing pairs: give away the least valuable card.
                CardView cheapest = null;
                int cheapestValue = int.MaxValue;
                foreach (CardView h in seat.Hand.Cards)
                {
                    int value = ScoreRules.Value(h.Card);
                    if (value < cheapestValue) { cheapestValue = value; cheapest = h; }
                }
                if (cheapest != null) PlaceOnTable(cheapest);
            }

            // Let the played card finish travelling before the next one appears.
            yield return new WaitForSeconds(opponentSettleTime);

            opponentRoutine = null; // BeginDraw queues the next routine into this slot
            BeginDraw();
        }

        IEnumerator OpponentResolveDraw(PlayerSeat seat)
        {
            // Long enough to read the card that just came off the pile.
            yield return new WaitForSeconds(opponentThinkTime);

            if (Phase != TurnPhase.ResolveDraw || CurrentSeat != seat) { opponentRoutine = null; yield break; }

            CardView drawn = DrawnCard;
            if (drawn == null) { opponentRoutine = null; NextSeat(); yield break; }

            CardView best = null;
            int bestValue = int.MinValue;
            foreach (CardView t in tableZone.Cards)
            {
                if (!CaptureRules.CanCapture(drawn.Card, t.Card)) continue;
                int value = ScoreRules.Value(t.Card);
                if (value > bestValue) { bestValue = value; best = t; }
            }

            if (best != null) ResolveDrawCapture(best);
            else LeaveDrawnOnTable();

            yield return new WaitForSeconds(opponentSettleTime);

            opponentRoutine = null;
            NextSeat();
        }

        void StopRoutines()
        {
            if (dealRoutine != null) { StopCoroutine(dealRoutine); dealRoutine = null; }
            if (opponentRoutine != null) { StopCoroutine(opponentRoutine); opponentRoutine = null; }
            IsDealing = false;
        }

        // ---------------------------------------------------------------- hints and buttons

        int CountTargets(Card probe)
        {
            int count = 0;
            foreach (CardView c in tableZone.Cards)
                if (c != null && CaptureRules.CanCapture(probe, c.Card)) count++;
            return count;
        }

        /// <summary>Rule ①: if anything in this hand can take something, a capture is compulsory.</summary>
        public bool HandHasAnyCapture(HandZone hand)
        {
            if (hand == null) return false;
            foreach (CardView h in hand.Cards)
                if (h != null && CountTargets(h.Card) > 0) return true;
            return false;
        }

        void Refresh()
        {
            RefreshCaptureHints();
            RefreshButtons();
        }

        void RefreshCaptureHints()
        {
            PlayerSeat seat = CurrentSeat;
            bool hasProbe = false;
            Card probe = default;

            if (Phase == TurnPhase.Play && seat != null && seat.IsLocal)
            {
                var selected = seat.Hand.GetSelected();
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
            PlayerSeat seat = CurrentSeat;

            // Buttons are the human's controls; opponents act on their own.
            if (seat == null || !seat.IsLocal)
            {
                HideButtons();
                return;
            }

            if (Phase == TurnPhase.Play)
            {
                continueButton.Show(false);
                placeButton.Show(!HandHasAnyCapture(seat.Hand) && seat.Hand.GetSelected().Count == 1);
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
            if (continueButton != null) continueButton.Show(false);
            if (placeButton != null) placeButton.Show(false);
        }

        void ClearCaptureHints()
        {
            foreach (CardView card in tableZone.Cards)
                if (card != null) card.SetHighlighted(false);
        }

        bool Validate()
        {
            bool seatsOk = seats != null && seats.Count > 0;
            if (seatsOk)
                foreach (PlayerSeat seat in seats)
                    if (seat == null || !seat.IsValid) { seatsOk = false; break; }

            if (seatsOk && library != null && cardPrefab != null && deckZone != null &&
                tableZone != null && drawSlot != null && continueButton != null && placeButton != null)
                return true;

            Debug.LogError("[PokerTable] Missing references — every seat needs a hand and a capture " +
                           "pile, and the table needs the library, card prefab, deck, table zone, " +
                           "draw slot and both buttons.", this);
            return false;
        }
    }
}
