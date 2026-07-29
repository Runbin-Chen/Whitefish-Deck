using UnityEngine;

namespace Whitefish.Poker
{
    /// <summary>
    /// One player's place at the table: their hand and their capture pile. The table deals to
    /// every seat and passes the turn around them, so adding a third or fourth is a scene job
    /// rather than a code one.
    /// </summary>
    public class PlayerSeat : MonoBehaviour
    {
        [SerializeField] string displayName = "Player";
        [SerializeField] HandZone hand;
        [SerializeField] PileZone capturePile;

        [Tooltip("A human plays this seat. Other seats take their turn on their own.")]
        [SerializeField] bool isLocal = true;

        [Tooltip("Debug aid: deal this seat's hand face up so the whole board can be read while testing.")]
        [SerializeField] bool revealHand;

        public string DisplayName => displayName;
        public HandZone Hand => hand;
        public PileZone CapturePile => capturePile;
        public bool IsLocal => isLocal;

        /// <summary>Face-up hands are for debugging; a real opponent keeps theirs hidden.</summary>
        public bool HandIsVisible => isLocal || revealHand;

        public int Score => capturePile != null ? ScoreRules.Total(capturePile.Cards) : 0;
        public int RedCards => capturePile != null ? ScoreRules.CountScoring(capturePile.Cards) : 0;

        public bool IsValid => hand != null && capturePile != null;

        public override string ToString() => displayName;
    }
}
