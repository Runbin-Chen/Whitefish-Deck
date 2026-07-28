using System.Collections;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// Runs a round of 钓红点. Right now that means the opening deal only: the local hand, then the
    /// face-up table spread, with whatever is left staying in the draw pile.
    ///
    /// Other players are not dealt yet, so the pile currently holds their cards too — compare
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

        public DealSettings DealSettings => dealSettings;
        public bool IsDealing { get; private set; }

        /// <summary>Raised once every card of the opening deal has been handed out.</summary>
        public event System.Action<PokerTable> RoundDealt;

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

            // Capturing (pair to ten, or match 10/J/Q/K) comes next; for now a hand card can
            // only be picked up and put back down.
            if (view.Zone == handZone)
                handZone.ToggleSelection(view);
        }

        void OnDeckClicked(DeckZone deck)
        {
            if (IsDealing) return;
            DealOne(handZone, faceUp: true);
        }

        bool Validate()
        {
            if (library != null && cardPrefab != null && deckZone != null && tableZone != null && handZone != null)
                return true;

            Debug.LogError("[PokerTable] Missing references — assign the library, card prefab and all three zones.", this);
            return false;
        }
    }
}
