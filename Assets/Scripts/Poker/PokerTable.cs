using System.Collections.Generic;
using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// Wires the framework together: fills the deck, scatters a spread across the felt and deals a
    /// hand to the player. This is the seam for game rules — everything it talks to is presentation.
    /// </summary>
    public class PokerTable : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] CardSpriteLibrary library;
        [SerializeField] CardView cardPrefab;
        [SerializeField] DeckZone deckZone;
        [SerializeField] TableZone tableZone;
        [SerializeField] HandZone handZone;

        [Header("Opening deal")]
        [SerializeField] int tableCardCount = 8;
        [SerializeField] int handCardCount = 5;
        [SerializeField] bool tableCardsFaceUp = true;

        [Header("Shuffle")]
        [SerializeField] bool useRandomSeed = true;
        [Tooltip("Used when the random seed is off, so a deal can be reproduced while debugging.")]
        [SerializeField] int shuffleSeed = 1;

        public DeckZone DeckZone => deckZone;
        public TableZone TableZone => tableZone;
        public HandZone HandZone => handZone;

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
            NewRound();
        }

        [ContextMenu("New Round")]
        public void NewRound()
        {
            if (!Validate()) return;

            tableZone.Clear();
            handZone.Clear();
            deckZone.ResetAndShuffle(useRandomSeed ? (int?)null : shuffleSeed);

            Deal(tableZone, tableCardCount, tableCardsFaceUp);
            Deal(handZone, handCardCount, true);
        }

        /// <summary>Draws from the deck into a zone. Stops early if the deck runs out.</summary>
        public List<CardView> Deal(CardZone zone, int count, bool faceUp)
        {
            var dealt = new List<CardView>(count);
            for (int i = 0; i < count; i++)
            {
                if (!deckZone.TryDraw(out Card card)) break;

                CardView view = SpawnCard(card, faceUp);
                zone.Add(view);
                dealt.Add(view);
            }
            return dealt;
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

        void OnCardClicked(CardView view)
        {
            if (view.Zone == handZone)
            {
                handZone.ToggleSelection(view);
            }
            else if (view.Zone == tableZone)
            {
                view.SetFaceUp(true);
                handZone.Add(view);
            }
        }

        void OnDeckClicked(DeckZone deck)
        {
            Deal(handZone, 1, true);
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
