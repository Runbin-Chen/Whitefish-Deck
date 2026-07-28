using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Whitefish.Poker
{
    /// <summary>
    /// Maps <see cref="Card"/> values onto sprites. Right-click the asset and choose
    /// "Rebuild From Source Folder" after changing art packs.
    /// </summary>
    [CreateAssetMenu(fileName = "CardSpriteLibrary", menuName = "Whitefish/Card Sprite Library")]
    public class CardSpriteLibrary : ScriptableObject
    {
        [Tooltip("52 faces, suit-major: clubs, diamonds, hearts, spades. Each suit runs 2..10, J, Q, K, A.")]
        [SerializeField] Sprite[] faces = new Sprite[Card.DeckSize];
        [SerializeField] Sprite back;
        [SerializeField] Sprite jokerRed;
        [SerializeField] Sprite jokerBlack;

        [Header("Rebuild")]
        [SerializeField] string sourceFolder = "Assets/Asset/kenney_playing-cards-pack/PNG/Cards (large)";

        public Sprite Back => back;
        public Sprite JokerRed => jokerRed;
        public Sprite JokerBlack => jokerBlack;

        public Sprite Face(Card card)
        {
            int i = card.Index;
            return faces != null && i >= 0 && i < faces.Length ? faces[i] : null;
        }

#if UNITY_EDITOR
        static readonly string[] SuitTokens = { "clubs", "diamonds", "hearts", "spades" };
        static readonly string[] RankTokens = { "02", "03", "04", "05", "06", "07", "08", "09", "10", "J", "Q", "K", "A" };

        [ContextMenu("Rebuild From Source Folder")]
        public void RebuildFromSourceFolder()
        {
            if (faces == null || faces.Length != Card.DeckSize)
                faces = new Sprite[Card.DeckSize];

            int found = 0;
            for (int s = 0; s < SuitTokens.Length; s++)
            {
                for (int r = 0; r < RankTokens.Length; r++)
                {
                    string path = $"{sourceFolder}/card_{SuitTokens[s]}_{RankTokens[r]}.png";
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                        Debug.LogWarning($"[CardSpriteLibrary] missing sprite: {path}", this);
                    else
                        found++;

                    faces[s * Card.RanksPerSuit + r] = sprite;
                }
            }

            back = AssetDatabase.LoadAssetAtPath<Sprite>($"{sourceFolder}/card_back.png");
            jokerRed = AssetDatabase.LoadAssetAtPath<Sprite>($"{sourceFolder}/card_joker_red.png");
            jokerBlack = AssetDatabase.LoadAssetAtPath<Sprite>($"{sourceFolder}/card_joker_black.png");

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            Debug.Log($"[CardSpriteLibrary] rebuilt: {found}/{Card.DeckSize} faces from {sourceFolder}", this);
        }
#endif
    }
}
