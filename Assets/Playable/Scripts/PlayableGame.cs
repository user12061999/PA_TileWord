using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PA.TileWord
{
    /// <summary>
    /// Playable Tile Word Explorer: Intro -> Tutorial (từ đầu tiên) -> Free play (các từ còn lại) -> End card.
    /// Danh sách từ, chủ đề, fill board và số từ để hiện CTA chỉnh được trong Luna Playground.
    /// Toàn bộ UI dựng bằng code (uGUI + TextMeshPro), chỉ dùng API Unity mà Luna Playworks hỗ trợ.
    /// </summary>
    public class PlayableGame : MonoBehaviour
    {
        [Header("Sprites (asset game gốc)")]
        public Sprite backgroundSprite;
        public Sprite tileSprite;
        public Sprite slotSprite;
        public Sprite headerSprite;
        public Sprite logoSprite;
        public Sprite buttonSprite;
        public Sprite handSprite;
        public Sprite sparkleSprite;

        [Header("Font")]
        public TMP_FontAsset font;

        [Header("Audio")]
        public AudioClip music;          // music_main: nhạc nền lặp
        public AudioClip sfxTap;         // tile click: chọn / bỏ chọn chữ
        public AudioClip sfxWrong;       // tile click blocked: chọn sai
        public AudioClip sfxWoodLand;    // crate crack: mảnh gỗ chạm chỗ (intro, fill board)
        public AudioClip sfxHeaderDrop;  // rubber quick: header trượt xuống
        public AudioClip sfxStreak;      // power up: vệt sáng khi ghép đủ từ
        public AudioClip sfxSlotFill;    // coin pickup: chữ bay vào ô card
        public AudioClip sfxWord;        // coins reward: card hoàn thành
        public AudioClip sfxWin;         // level complete: hiện end card
        public AudioClip sfxButton;      // ui_button_1: bấm Continue / CTA

        [Range(0f, 1f)] public float musicVolume = 0.45f;

        [Header("Tuning")]
        public float introDuration = 1.5f;
        [Tooltip("Sau tutorial, bàn tay gợi ý chỉ hiện lại khi người chơi không tương tác trong khoảng này (giây).")]
        public float idleHintDelay = 6f;

        [Header("Luna Playground")]
        [LunaPlaygroundField("Words (first = tutorial)", 0, "Words")]
        [LunaPlaygroundFieldArrayLength(1, MaxWords)]
        [Tooltip("Danh sách từ cần tìm (tối đa 8, mỗi từ 2–9 chữ A–Z). Từ đầu tiên dùng cho tutorial.")]
        public string[] words = { "CAT", "DOG", "FISH", "BIRD", "BUNNY", "TURTLE" };

        [LunaPlaygroundField("Refill board after a word", 0, "Gameplay")]
        [Tooltip("Sau khi clear 1 từ: các ô phía trên rơi xuống lấp chỗ trống và chữ mới rơi từ trên vào.")]
        public bool refillBoard = true;

        [LunaPlaygroundField("Words completed to show CTA", 1, "Gameplay")]
        [LunaPlaygroundFieldStep(1)]
        [Range(1, MaxWords)]
        [Tooltip("Số từ (tính cả từ tutorial) người chơi cần hoàn thành thì hiện end card. 1 = ngay sau tutorial.")]
        public int wordsToShowCta = 3;

        // ---------------------------------------------------------------- data
        private const float RefWidth = 1080f;
        private const float RefHeight = 1920f;
        private const int GridSize = 10;
        private const float CellW = 98f;
        private const float CellH = 100f;
        private const float TileW = 92f;
        private const float TileH = 100f;
        private const int MaxWords = 8;
        private const int MinWordLength = 2;
        private const int MaxWordLength = 9;
        private const int MaxTotalLetters = 60;
        private const int BoardSeed = 20261124;
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string FallbackFiller = "BEJKLMNPQRUVWYZ";
        private static readonly string[] DefaultWords = { "CAT", "DOG", "FISH" };

        // Danh sách từ đã chuẩn hoá từ config (in hoa, chỉ A–Z). Từ 0 là tutorial.
        private string[] _words;
        private int[][] _wordCounts;
        // Chữ "nhiễu" lấp board: các chữ không xuất hiện trong bất kỳ từ nào để người chơi không bị nhầm.
        private string _filler;
        private float _slotSize = 60f;
        private float _cardsHeight;
        private RectTransform _cardStrip;
        private List<int> _cardOrder;
        private int _layoutVersion;
        private float _messageY = -650f;

        private static readonly Color Brown = new Color32(107, 58, 30, 255);
        private static readonly Color FoundSlotColor = new Color(0.78f, 0.95f, 0.55f, 1f);
        private static readonly Color StreakColor = new Color(1f, 0.93f, 0.55f, 0.95f);

        private enum Phase { Intro, Tutorial, Free, End }

        private class WordCard
        {
            public RectTransform Rect;
            public Image[] Slots;
            public TextMeshProUGUI[] Labels;
            public bool Found;
            public CanvasGroup Group;
        }

        // ---------------------------------------------------------------- runtime
        private RectTransform _canvasRect;
        private CanvasScaler _scaler;
        private RectTransform _background;
        private RectTransform _root;
        private RectTransform _rootTop;
        private RectTransform _header;
        private RectTransform _board;
        private RectTransform _focusLayer;
        private RectTransform _fxLayer;
        private RectTransform _hand;
        private CanvasGroup _overlay;
        private TextMeshProUGUI _message;
        private CanvasGroup _messageGroup;
        private const int SfxVoices = 8;
        private AudioSource[] _sfxSources;
        private int _nextSfx;
        private AudioSource _music;

        private LetterTile[,] _tiles;
        private LetterTile[][] _wordTiles;
        private WordCard[] _cards;
        private readonly List<LetterTile> _selection = new List<LetterTile>();

        private Phase _phase = Phase.Intro;
        private int _foundCount;
        private int _flyingLetters;
        private int _refilling;
        private float _idleTime;
        private RectTransform _handTarget;
        private Coroutine _handLoop;
        private bool _storeOpened;

        // ================================================================ lifecycle
        private void Start()
        {
            Application.targetFrameRate = 60;
            PrepareWords();
            SetupAudio();

            EnsureEventSystem();
            BuildCanvas();
            BuildHeader();
            BuildBoard();
            // Header vẽ đè lên board: chữ mới (fill) và mảnh gỗ intro rơi xuống từ phía sau header.
            _header.SetAsLastSibling();
            BuildTopLayers();
            UpdateLayout();

            StartCoroutine(Flow());
        }

        private void Update()
        {
            UpdateLayout();
            UpdateCardClipping();

            if (_phase == Phase.Free && _handTarget == null && _flyingLetters == 0 && _refilling == 0)
            {
                _idleTime += Time.deltaTime;
                if (_idleTime >= idleHintDelay)
                {
                    LetterTile next = NextHintTile();
                    if (next != null) ShowHand(next.Rect);
                }
            }
        }

        /// <summary>Số từ phải hoàn thành để hiện CTA, kẹp trong [1, số từ] (từ tutorial tính là 1).</summary>
        private int WordsToShowCta()
        {
            return Mathf.Clamp(wordsToShowCta, 1, _words.Length);
        }

        // ================================================================ words config
        /// <summary>Chuẩn hoá danh sách từ từ Luna: in hoa, chỉ giữ A–Z, bỏ từ quá ngắn/dài, tối đa 8 từ và 60 chữ.</summary>
        private void PrepareWords()
        {
            List<string> list = new List<string>();
            int totalLetters = 0;
            if (words != null)
            {
                for (int i = 0; i < words.Length && list.Count < MaxWords; i++)
                {
                    string clean = CleanWord(words[i]);
                    if (clean.Length < MinWordLength || clean.Length > MaxWordLength) continue;
                    if (totalLetters + clean.Length > MaxTotalLetters) continue;
                    if (list.Contains(clean)) continue;
                    list.Add(clean);
                    totalLetters += clean.Length;
                }
            }
            if (list.Count == 0)
            {
                Debug.LogWarning("[Playable] Danh sách từ trống/không hợp lệ, dùng mặc định CAT, DOG, FISH.");
                for (int i = 0; i < DefaultWords.Length; i++) list.Add(DefaultWords[i]);
            }
            _words = list.ToArray();
            _wordCounts = new int[_words.Length][];
            for (int w = 0; w < _words.Length; w++) _wordCounts[w] = LetterCounts(_words[w]);

            _filler = "";
            for (int i = 0; i < Alphabet.Length; i++)
            {
                char ch = Alphabet[i];
                bool inWord = false;
                for (int w = 0; w < _words.Length && !inWord; w++)
                {
                    if (_words[w].IndexOf(ch) >= 0) inWord = true;
                }
                if (!inWord) _filler += ch;
            }
            // Bộ từ dùng gần hết bảng chữ cái: chấp nhận chữ nhiễu trùng với chữ trong từ.
            if (_filler.Length < 4) _filler = FallbackFiller;
        }

        private static string CleanWord(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string upper = raw.ToUpper();
            string clean = "";
            for (int i = 0; i < upper.Length; i++)
            {
                char ch = upper[i];
                if (ch >= 'A' && ch <= 'Z') clean += ch;
            }
            return clean;
        }

        private static int[] LetterCounts(string s)
        {
            int[] counts = new int[26];
            for (int i = 0; i < s.Length; i++) counts[s[i] - 'A']++;
            return counts;
        }

        /// <summary>Đúng nếu mọi chữ trong <paramref name="part"/> đều có đủ số lượng trong <paramref name="whole"/>.</summary>
        private static bool IsSubset(int[] part, int[] whole)
        {
            for (int i = 0; i < 26; i++)
            {
                if (part[i] > whole[i]) return false;
            }
            return true;
        }

        private bool WordAllowed(int w)
        {
            if (_cards[w].Found) return false;
            if (_phase == Phase.Tutorial && w != 0) return false;
            return true;
        }

        private IEnumerator Flow()
        {
            yield return StartCoroutine(Intro());
            yield return StartCoroutine(Tutorial());

            int target = WordsToShowCta();
            if (target > 1)
            {
                _phase = Phase.Free;
                _idleTime = 0f;
                int remaining = target - 1;
                ShowMessage("Great! Find " + remaining + (remaining == 1 ? " more word" : " more words"));
            }

            while (_foundCount < target) yield return null;
            _phase = Phase.End;
            HideHand();
            while (_flyingLetters > 0) yield return null;

            yield return new WaitForSeconds(0.6f);
            yield return StartCoroutine(EndCard());
        }

        // ================================================================ intro
        private IEnumerator Intro()
        {
            _phase = Phase.Intro;
            _messageGroup.alpha = 0f;

            int count = GridSize * GridSize;
            Vector2[] from = new Vector2[count];
            float[] delay = new float[count];
            Vector2[] to = new Vector2[count];

            // Mảnh gỗ rơi từ trên xuống lần lượt từng mảnh theo kiểu "rắn":
            // hàng dưới cùng x tăng dần (trái -> phải), hết hàng thì lên hàng trên và đi x giảm dần
            // (phải -> trái), cứ xen kẽ như vậy tới hàng trên cùng.
            const float fallTime = 0.35f;
            const float fallHeight = 1400f;
            int maxOrder = GridSize * GridSize - 1;
            float step = Mathf.Max(0.01f, (introDuration - fallTime - 0.05f) / maxOrder);

            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    int i = r * GridSize + c;
                    LetterTile tile = _tiles[r, c];
                    int rowFromBottom = GridSize - 1 - r;
                    to[i] = tile.Rect.anchoredPosition;
                    from[i] = to[i] + new Vector2(0f, fallHeight);
                    int xOrder = rowFromBottom % 2 == 0 ? c : GridSize - 1 - c;
                    delay[i] = (rowFromBottom * GridSize + xOrder) * step;
                    tile.Rect.anchoredPosition = from[i];
                    tile.Rect.localRotation = Quaternion.identity;
                    tile.Rect.localScale = Vector3.one;
                }
            }

            Vector2 headerHome = _header.anchoredPosition;
            _header.anchoredPosition = headerHome + new Vector2(0f, 600f);

            bool[] landed = new bool[count];
            int landedCount = 0;
            bool headerSoundPlayed = false;

            float time = 0f;
            bool done = false;
            while (!done)
            {
                time += Time.deltaTime;
                done = true;
                bool landSoundThisFrame = false;
                for (int i = 0; i < count; i++)
                {
                    LetterTile tile = _tiles[i / GridSize, i % GridSize];
                    float t = Mathf.Clamp01((time - delay[i]) / fallTime);
                    if (t < 1f) done = false;
                    tile.Rect.anchoredPosition = Vector2.LerpUnclamped(from[i], to[i], PATween.OutBounce(t));

                    // Tiếng gỗ khi chạm chỗ (OutBounce chạm đích ở t = 0.7): cứ 3 mảnh phát 1 lần, tối đa 1 lần/frame.
                    if (!landed[i] && t >= 0.7f)
                    {
                        landed[i] = true;
                        landedCount++;
                        if (!landSoundThisFrame && landedCount % 3 == 1)
                        {
                            landSoundThisFrame = true;
                            Play(sfxWoodLand, 0.45f, Random.Range(0.9f, 1.15f));
                        }
                    }
                }

                if (!headerSoundPlayed && time >= introDuration * 0.35f)
                {
                    headerSoundPlayed = true;
                    Play(sfxHeaderDrop, 0.8f, 1f);
                }

                float h = Mathf.Clamp01((time - introDuration * 0.35f) / 0.5f);
                if (h < 1f) done = false;
                _header.anchoredPosition = Vector2.LerpUnclamped(headerHome + new Vector2(0f, 600f), headerHome, PATween.OutBack(h));
                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                LetterTile tile = _tiles[i / GridSize, i % GridSize];
                tile.Rect.anchoredPosition = to[i];
                tile.Rect.localRotation = Quaternion.identity;
                tile.Rect.localScale = Vector3.one;
            }
            _header.anchoredPosition = headerHome;
        }

        // ================================================================ tutorial
        private IEnumerator Tutorial()
        {
            _phase = Phase.Tutorial;
            LetterTile[] focus = _wordTiles[0];
            for (int i = 0; i < focus.Length; i++)
            {
                focus[i].Rect.SetParent(_focusLayer, true);
            }

            _overlay.gameObject.SetActive(true);
            _overlay.blocksRaycasts = true;
            ShowMessage("Tap the letters to find " + _words[0] + "!");
            yield return StartCoroutine(PATween.Fade(_overlay, 0f, 1f, 0.3f));
            RefreshTutorialHand();

            while (!_cards[0].Found) yield return null;
        }

        // ================================================================ input
        private void OnTileTapped(LetterTile tile)
        {
            if (_phase != Phase.Tutorial && _phase != Phase.Free) return;
            if (tile.Used) return;
            if (_refilling > 0) return;

            _idleTime = 0f;
            HideHand();

            int index = _selection.IndexOf(tile);
            if (index >= 0)
            {
                // Chạm lại một chữ đã chọn để bỏ chọn (thứ tự không quan trọng nên bỏ chữ nào cũng được).
                _selection.RemoveAt(index);
                tile.SetSelected(false);
                Play(sfxTap, 0.9f, 0.85f);
                RefreshTutorialHand();
                return;
            }

            // Không cần đúng thứ tự: hợp lệ khi các chữ đã chọn (tính cả số lần lặp) nằm trọn trong một từ chưa tìm.
            string candidate = SelectionString() + tile.Letter;
            int[] candidateCounts = LetterCounts(candidate);
            int completed = -1;
            bool isValid = false;
            for (int w = 0; w < _words.Length; w++)
            {
                if (!WordAllowed(w)) continue;
                if (!IsSubset(candidateCounts, _wordCounts[w])) continue;
                isValid = true;
                if (_words[w].Length == candidate.Length)
                {
                    completed = w;
                    break;
                }
            }

            if (!isValid)
            {
                tile.Shake();
                Play(sfxWrong);
                RefreshTutorialHand();
                return;
            }

            _selection.Add(tile);
            tile.SetSelected(true);
            // Pitch tăng dần theo số chữ đã chọn: nghe "lên nốt" khi gần ghép xong từ.
            Play(sfxTap, 1f, 1f + 0.1f * (_selection.Count - 1));

            if (completed >= 0)
            {
                List<LetterTile> word = new List<LetterTile>(_selection);
                _selection.Clear();
                StartCoroutine(CompleteWord(completed, word));
            }
            else
            {
                RefreshTutorialHand();
            }
        }

        private void RefreshTutorialHand()
        {
            if (_phase != Phase.Tutorial) return;
            LetterTile next = NextHintTile();
            if (next != null) ShowHand(next.Rect);
        }

        /// <summary>
        /// Ô chữ tiếp theo nên chỉ: chọn từ chưa tìm khớp với các chữ đang chọn (hoặc từ đầu tiên chưa tìm),
        /// rồi lấy chữ còn thiếu đầu tiên theo thứ tự trong từ.
        /// </summary>
        private LetterTile NextHintTile()
        {
            int[] selected = LetterCounts(SelectionString());
            for (int w = 0; w < _words.Length; w++)
            {
                if (!WordAllowed(w)) continue;
                if (!IsSubset(selected, _wordCounts[w])) continue;

                int[] needed = new int[26];
                for (int k = 0; k < 26; k++) needed[k] = _wordCounts[w][k] - selected[k];

                string word = _words[w];
                for (int i = 0; i < word.Length; i++)
                {
                    int li = word[i] - 'A';
                    if (needed[li] <= 0) continue;
                    LetterTile placed = _wordTiles[w][i];
                    if (placed != null && !placed.Used && !placed.Selected && placed.Letter == word[i]) return placed;
                    LetterTile any = FindTile(word[i], null, false);
                    if (any != null) return any;
                }
            }
            return null;
        }

        /// <summary>Tìm ô trên board có chữ <paramref name="letter"/>, chưa dùng, không nằm trong <paramref name="exclude"/>.</summary>
        private LetterTile FindTile(char letter, List<LetterTile> exclude, bool allowSelected)
        {
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    LetterTile t = _tiles[r, c];
                    if (t == null || t.Used || t.Letter != letter) continue;
                    if (!allowSelected && t.Selected) continue;
                    if (exclude != null && exclude.Contains(t)) continue;
                    return t;
                }
            }
            return null;
        }

        private string SelectionString()
        {
            string s = "";
            for (int i = 0; i < _selection.Count; i++) s += _selection[i].Letter;
            return s;
        }

        /// <summary>
        /// Sau khi một từ được clear, đảm bảo mọi từ chưa tìm vẫn còn đủ ô trên board (ô của chúng có thể đã bị
        /// người chơi dùng cho từ khác có chung chữ). Thiếu thì lấy ô cùng chữ khác, không có thì đổi một ô chữ nhiễu.
        /// </summary>
        private void EnsureSolvable()
        {
            List<LetterTile> claimed = new List<LetterTile>();
            for (int w = 0; w < _words.Length; w++)
            {
                if (_cards[w].Found) continue;
                string word = _words[w];
                for (int i = 0; i < word.Length; i++)
                {
                    LetterTile t = _wordTiles[w][i];
                    bool ok = t != null && !t.Used && t.Letter == word[i] && !claimed.Contains(t);
                    if (!ok) t = FindTile(word[i], claimed, true);
                    if (t == null) t = ConvertFillerTile(word[i], claimed);
                    if (t != null) claimed.Add(t);
                    _wordTiles[w][i] = t;
                }
            }
        }

        private LetterTile ConvertFillerTile(char letter, List<LetterTile> claimed)
        {
            List<LetterTile> candidates = new List<LetterTile>();
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    LetterTile t = _tiles[r, c];
                    if (t == null || t.Used || t.Selected || claimed.Contains(t)) continue;
                    if (_filler.IndexOf(t.Letter) < 0) continue;
                    candidates.Add(t);
                }
            }
            if (candidates.Count == 0) return null;
            LetterTile pick = candidates[Random.Range(0, candidates.Count)];
            pick.Letter = letter;
            pick.Label.text = letter.ToString();
            return pick;
        }

        // ================================================================ word complete
        private IEnumerator CompleteWord(int wordIndex, List<LetterTile> letters)
        {
            WordCard card = _cards[wordIndex];
            card.Found = true;
            _flyingLetters += letters.Count;
            bool wasTutorial = _phase == Phase.Tutorial;
            Play(sfxStreak, 0.8f, 1f);
            BringCardToFront(wordIndex);

            for (int i = 0; i < letters.Count; i++)
            {
                LetterTile used = letters[i];
                used.Used = true;
                used.Rect.SetParent(_fxLayer, true);
                if (_tiles[used.Row, used.Col] == used) _tiles[used.Row, used.Col] = null;
            }

            // 1) Vệt sáng nối các chữ.
            List<Image> beams = new List<Image>();
            for (int i = 0; i < letters.Count; i++)
            {
                SpawnSparkle(letters[i].Rect.anchoredPosition, 110f, 0.45f);
                if (i == letters.Count - 1) break;
                Vector2 a = letters[i].Rect.anchoredPosition;
                Vector2 b = letters[i + 1].Rect.anchoredPosition;
                Image beam = CreateBeam(a, b);
                beams.Add(beam);
                yield return StartCoroutine(GrowBeam(beam, (b - a).magnitude, 0.09f));
            }
            for (int i = 0; i < letters.Count; i++) letters[i].Rect.SetAsLastSibling();

            if (wasTutorial)
            {
                StartCoroutine(HideOverlay());
            }
            yield return new WaitForSeconds(0.12f);
            StartCoroutine(FadeAndDestroyBeams(beams, 0.25f));

            // 2) Chữ bay về card: mỗi chữ vào ô có cùng chữ cái (người chơi có thể chọn theo thứ tự bất kỳ).
            string word = _words[wordIndex];
            bool[] slotTaken = new bool[word.Length];
            for (int i = 0; i < letters.Count; i++)
            {
                int slot = -1;
                for (int s = 0; s < word.Length; s++)
                {
                    if (!slotTaken[s] && word[s] == letters[i].Letter)
                    {
                        slot = s;
                        break;
                    }
                }
                if (slot < 0) slot = i;
                slotTaken[slot] = true;
                StartCoroutine(FlyLetter(letters[i], card, slot, i * 0.08f));
            }

            // 3) Fill board: ô phía trên rơi xuống lấp chỗ trống, chữ mới rơi từ trên vào.
            //    RefillBoard cập nhật lưới ngay trong lần chạy đầu (trước yield), nên EnsureSolvable thấy lưới mới.
            if (refillBoard) StartCoroutine(RefillBoard());
            EnsureSolvable();

            while (CountFilled(card) < letters.Count) yield return null;

            Play(sfxWord);
            Vector2 cardPos = (Vector2)_fxLayer.InverseTransformPoint(card.Rect.position);
            for (int k = 0; k < 8; k++)
            {
                Vector2 offset = Random.insideUnitCircle * new Vector2(card.Rect.rect.width * 0.6f, 50f);
                SpawnSparkle(cardPos + offset, Random.Range(50f, 90f), Random.Range(0.4f, 0.7f));
            }
            yield return StartCoroutine(PATween.Punch(card.Rect, 0.2f, 0.3f));
            StartCoroutine(RemoveCard(wordIndex));

            _foundCount++;
            _idleTime = 0f;
        }

        private IEnumerator RefillBoard()
        {
            _refilling++;

            List<LetterTile> moving = new List<LetterTile>();
            List<Vector2> fromPos = new List<Vector2>();
            List<Vector2> toPos = new List<Vector2>();
            List<float> delays = new List<float>();

            for (int c = 0; c < GridSize; c++)
            {
                float columnDelay = c * 0.025f;

                // Dồn các ô còn lại xuống đáy cột.
                int write = GridSize - 1;
                for (int r = GridSize - 1; r >= 0; r--)
                {
                    LetterTile tile = _tiles[r, c];
                    if (tile == null) continue;
                    if (r != write)
                    {
                        _tiles[write, c] = tile;
                        _tiles[r, c] = null;
                        tile.Row = write;
                        moving.Add(tile);
                        fromPos.Add(tile.Rect.anchoredPosition);
                        toPos.Add(CellPosition(write, c));
                        delays.Add(columnDelay);
                    }
                    write--;
                }

                // Chữ mới rơi từ phía trên board vào các ô trống trên cùng.
                int empty = write + 1;
                for (int r = write; r >= 0; r--)
                {
                    Vector2 target = CellPosition(r, c);
                    Vector2 start = target + new Vector2(0f, empty * CellH + 120f);
                    char letter = _filler[Random.Range(0, _filler.Length)];
                    LetterTile tile = CreateTile(letter, r, c, start);
                    _tiles[r, c] = tile;
                    moving.Add(tile);
                    fromPos.Add(start);
                    toPos.Add(target);
                    delays.Add(columnDelay + 0.06f + (write - r) * 0.04f);
                }
            }

            const float fallTime = 0.35f;
            float maxDelay = 0f;
            for (int i = 0; i < delays.Count; i++) maxDelay = Mathf.Max(maxDelay, delays[i]);

            // Tiếng gỗ chạm chỗ: mỗi cột phát 1 lần (khi ô đầu tiên của cột chạm đích), tối đa 1 lần/frame.
            bool[] columnSounded = new bool[GridSize];

            float time = 0f;
            while (time < maxDelay + fallTime)
            {
                time += Time.deltaTime;
                bool soundThisFrame = false;
                for (int i = 0; i < moving.Count; i++)
                {
                    if (moving[i] == null) continue;
                    float t = Mathf.Clamp01((time - delays[i]) / fallTime);
                    moving[i].Rect.anchoredPosition = Vector2.LerpUnclamped(fromPos[i], toPos[i], PATween.OutBounce(t));

                    int col = moving[i].Col;
                    if (t >= 0.7f && !columnSounded[col] && !soundThisFrame)
                    {
                        columnSounded[col] = true;
                        soundThisFrame = true;
                        Play(sfxWoodLand, 0.4f, Random.Range(0.95f, 1.15f));
                    }
                }
                yield return null;
            }
            for (int i = 0; i < moving.Count; i++)
            {
                if (moving[i] != null) moving[i].Rect.anchoredPosition = toPos[i];
            }

            _refilling--;
        }

        private IEnumerator HideOverlay()
        {
            yield return StartCoroutine(PATween.Fade(_overlay, _overlay.alpha, 0f, 0.3f));
            _overlay.blocksRaycasts = false;
            _overlay.gameObject.SetActive(false);
        }

        private int CountFilled(WordCard card)
        {
            int n = 0;
            for (int i = 0; i < card.Labels.Length; i++)
            {
                if (card.Labels[i].alpha > 0.99f) n++;
            }
            return n;
        }

        private IEnumerator FlyLetter(LetterTile tile, WordCard card, int slotIndex, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            RectTransform slot = (RectTransform)card.Slots[slotIndex].transform;
            Vector2 p0 = tile.Rect.anchoredPosition;
            Vector2 p2 = _fxLayer.InverseTransformPoint(slot.position);
            Vector2 p1 = (p0 + p2) * 0.5f + new Vector2(Random.Range(-160f, 160f), 260f);
            float startScale = tile.Rect.localScale.x;
            float endScale = _slotSize / TileH;

            const float duration = 0.55f;
            float time = 0f;
            float trailTimer = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = PATween.InOutSine(Mathf.Clamp01(time / duration));
                float u = 1f - t;
                Vector2 pos = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                tile.Rect.anchoredPosition = pos;
                float s = Mathf.Lerp(startScale, endScale, t);
                tile.Rect.localScale = new Vector3(s, s, 1f);

                trailTimer += Time.deltaTime;
                if (trailTimer >= 0.02f)
                {
                    trailTimer = 0f;
                    SpawnSparkle(pos, Random.Range(45f, 70f), 0.35f);
                }
                yield return null;
            }

            Destroy(tile.gameObject);
            card.Labels[slotIndex].alpha = 1f;
            card.Slots[slotIndex].color = FoundSlotColor;
            Play(sfxSlotFill, 0.7f, 1f + 0.08f * slotIndex);
            SpawnSparkle(p2, 90f, 0.35f);
            _flyingLetters--;
            yield return StartCoroutine(PATween.Punch(slot, 0.3f, 0.2f));
        }

        private Image CreateBeam(Vector2 from, Vector2 to)
        {
            RectTransform rt = NewRect("Beam", _fxLayer);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = from;
            Vector2 d = to - from;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            rt.sizeDelta = new Vector2(0f, 22f);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = StreakColor;
            img.raycastTarget = false;
            return img;
        }

        private IEnumerator GrowBeam(Image beam, float length, float duration)
        {
            RectTransform rt = beam.rectTransform;
            yield return StartCoroutine(PATween.Run(duration, PATween.OutCubic, delegate (float t)
            {
                rt.sizeDelta = new Vector2(length * t, 22f);
            }));
        }

        private IEnumerator FadeAndDestroyBeams(List<Image> beams, float duration)
        {
            yield return StartCoroutine(PATween.Run(duration, PATween.Linear, delegate (float t)
            {
                for (int i = 0; i < beams.Count; i++)
                {
                    Color c = StreakColor;
                    c.a *= 1f - t;
                    beams[i].color = c;
                }
            }));
            for (int i = 0; i < beams.Count; i++) Destroy(beams[i].gameObject);
        }

        private void SpawnSparkle(Vector2 pos, float size, float life)
        {
            Image img = NewImage("Sparkle", _fxLayer, sparkleSprite, new Vector2(size, size), pos);
            img.color = StreakColor;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            img.transform.SetAsFirstSibling();
            StartCoroutine(SparkleRoutine(img, life));
        }

        private IEnumerator SparkleRoutine(Image img, float life)
        {
            Color baseColor = img.color;
            yield return StartCoroutine(PATween.Run(life, PATween.Linear, delegate (float t)
            {
                if (img == null) return;
                Color c = baseColor;
                c.a = baseColor.a * (1f - t);
                img.color = c;
                float s = Mathf.Lerp(1f, 0.3f, t);
                img.rectTransform.localScale = new Vector3(s, s, 1f);
            }));
            if (img != null) Destroy(img.gameObject);
        }

        // ================================================================ hand
        private void ShowHand(RectTransform target)
        {
            _handTarget = target;
            _hand.gameObject.SetActive(true);
            if (_handLoop == null) _handLoop = StartCoroutine(HandLoop());
        }

        private void HideHand()
        {
            _handTarget = null;
            if (_handLoop != null)
            {
                StopCoroutine(_handLoop);
                _handLoop = null;
            }
            _hand.gameObject.SetActive(false);
        }

        private IEnumerator HandLoop()
        {
            Vector2 away = new Vector2(70f, -80f);
            while (_handTarget != null)
            {
                float time = 0f;
                const float cycle = 1.1f;
                while (time < cycle && _handTarget != null)
                {
                    time += Time.deltaTime;
                    Vector2 targetPos = (Vector2)_rootTop.InverseTransformPoint(_handTarget.position) + new Vector2(8f, -22f);
                    Vector2 offset;
                    float scale;
                    if (time < 0.35f)
                    {
                        float t = PATween.OutCubic(time / 0.35f);
                        offset = Vector2.Lerp(away, Vector2.zero, t);
                        scale = 1f;
                    }
                    else if (time < 0.6f)
                    {
                        float t = (time - 0.35f) / 0.25f;
                        offset = Vector2.zero;
                        scale = 1f - Mathf.Sin(t * Mathf.PI) * 0.15f;
                    }
                    else
                    {
                        offset = Vector2.zero;
                        scale = 1f;
                    }
                    _hand.anchoredPosition = targetPos + offset;
                    _hand.localScale = new Vector3(scale, scale, 1f);
                    yield return null;
                }
            }
            _handLoop = null;
        }

        // ================================================================ end card
        private IEnumerator EndCard()
        {
            Play(sfxWin);
            StartCoroutine(FadeMusic(musicVolume * 0.4f, 0.8f));
            StoreBridge.GameEnded();

            RectTransform end = NewRect("EndCard", _canvasRect);
            Stretch(end);
            CanvasGroup group = end.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image dim = end.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);
            dim.raycastTarget = true;
            Button anywhere = end.gameObject.AddComponent<Button>();
            anywhere.transition = Selectable.Transition.None;
            anywhere.onClick.AddListener(OpenStore);

            RectTransform content = NewRect("Content", end);
            content.sizeDelta = new Vector2(RefWidth, RefHeight);

            Image logo = NewImage("Logo", content, logoSprite, new Vector2(860f, 860f * 719f / 1050f), new Vector2(0f, 260f));
            logo.rectTransform.localScale = Vector3.zero;

            // Nút cam kiểu PLAY của màn Home gốc (sprite PlayButton 537x237).
            Vector2 buttonSize = new Vector2(640f, 640f * 237f / 537f);
            Image button = NewImage("ContinueButton", content, buttonSprite, buttonSize, new Vector2(0f, -340f));
            button.raycastTarget = true;
            Button continueButton = button.gameObject.AddComponent<Button>();
            continueButton.transition = Selectable.Transition.None;
            continueButton.onClick.AddListener(OpenStore);

            // Chữ trắng viền cam đậm: viền giả bằng 8 bản sao lệch quanh chữ chính (không cần material outline của TMP).
            Color outline = new Color32(206, 84, 8, 255);
            Vector2 labelSize = new Vector2(600f, 200f);
            Vector2 labelPos = new Vector2(0f, 10f);
            const float o = 5f;
            Vector2[] offsets =
            {
                new Vector2(-o, 0f), new Vector2(o, 0f), new Vector2(0f, -o), new Vector2(0f, o),
                new Vector2(-o, -o), new Vector2(o, -o), new Vector2(-o, o), new Vector2(o, o),
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                NewText("LabelOutline", button.transform, "CONTINUE", 84f, outline, labelSize, labelPos + offsets[i]);
            }
            NewText("LabelShadow", button.transform, "CONTINUE", 84f, outline, labelSize, labelPos + new Vector2(0f, -o * 2f));
            NewText("Label", button.transform, "CONTINUE", 84f, Color.white, labelSize, labelPos);
            button.rectTransform.localScale = Vector3.zero;

            yield return StartCoroutine(PATween.Fade(group, 0f, 1f, 0.3f));
            yield return StartCoroutine(PATween.Scale(logo.rectTransform, Vector3.zero, Vector3.one, 0.45f, PATween.OutBack));
            yield return StartCoroutine(PATween.Scale(button.rectTransform, Vector3.zero, Vector3.one, 0.35f, PATween.OutBack));

            // Nút Continue nhịp nhàng để mời bấm.
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;
                float s = 1f + Mathf.Sin(time * 5f) * 0.06f;
                button.rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private void OpenStore()
        {
            if (!_storeOpened) Play(sfxButton, 0.9f, 1f);
            _storeOpened = true;
            StoreBridge.InstallFullGame();
        }

        // ================================================================ build UI
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void BuildCanvas()
        {
            GameObject go = new GameObject("PlayableCanvas");
            go.layer = 5;
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)go.transform;

            Image bg = NewImage("Background", _canvasRect, backgroundSprite, new Vector2(RefWidth, RefHeight), Vector2.zero);
            _background = bg.rectTransform;

            _root = NewRect("Root", _canvasRect);
            _root.sizeDelta = new Vector2(RefWidth, RefHeight);
        }

        private void BuildHeader()
        {
            float width = 1000f;
            float height = width * 414f / 1080f;
            float k = height / 414f;
            Image panel = NewImage("Header", _root, headerSprite, new Vector2(width, height), new Vector2(0f, RefHeight * 0.5f - 40f - height * 0.5f));
            _header = panel.rectTransform;

            // Dải cam (y 225..285 trên ảnh 414px): tiêu đề "FIND THE WORDS".
            float bandY = height * 0.5f - 255f * k;
            NewText("Title", _header, "FIND THE WORDS", 46f, Color.white, new Vector2(800f, 60f), new Vector2(-40f, bandY + 2f));

            // Dải kem (y 290..378): các thẻ từ xếp thành MỘT hàng. Thẻ vượt quá mép phải bị che (RectMask2D)
            // và lộ dần ra khi các thẻ phía trước được clear.
            float creamY = height * 0.5f - 334f * k;
            BuildCards(creamY);
        }

        private const float CardSlotGap = 4f;
        private const float CardGap = 30f;
        private const float StripWidth = 890f;
        private const float StripHeight = 80f;
        private const float StripPadding = 14f;

        private void BuildCards(float creamY)
        {
            _slotSize = 56f;
            _cardsHeight = 0f;

            _cardStrip = NewRect("CardStrip", _header);
            _cardStrip.sizeDelta = new Vector2(StripWidth, StripHeight);
            _cardStrip.anchoredPosition = new Vector2(0f, creamY);
            _cardStrip.gameObject.AddComponent<RectMask2D>();

            _cards = new WordCard[_words.Length];
            _cardOrder = new List<int>();
            for (int w = 0; w < _words.Length; w++)
            {
                string word = _words[w];
                float cardWidth = CardWidth(word);
                RectTransform cardRect = NewRect("Card_" + word, _cardStrip);
                cardRect.sizeDelta = new Vector2(cardWidth, _slotSize);

                WordCard card = new WordCard();
                card.Rect = cardRect;
                card.Group = cardRect.gameObject.AddComponent<CanvasGroup>();
                card.Group.blocksRaycasts = false;
                card.Slots = new Image[word.Length];
                card.Labels = new TextMeshProUGUI[word.Length];
                for (int i = 0; i < word.Length; i++)
                {
                    float sx = -cardWidth * 0.5f + _slotSize * 0.5f + i * (_slotSize + CardSlotGap);
                    Image slot = NewImage("Slot" + i, cardRect, slotSprite, new Vector2(_slotSize, _slotSize), new Vector2(sx, 0f));
                    TextMeshProUGUI label = NewText("Letter", slot.transform, word[i].ToString(), _slotSize * 0.64f, Brown, new Vector2(_slotSize, _slotSize), new Vector2(0f, 1f));
                    label.alpha = 0.3f;
                    card.Slots[i] = slot;
                    card.Labels[i] = label;
                }
                _cards[w] = card;
                _cardOrder.Add(w);
            }
            LayoutCards(false);
        }

        private float CardWidth(string word)
        {
            return word.Length * _slotSize + (word.Length - 1) * CardSlotGap;
        }

        /// <summary>Xếp các thẻ trong _cardOrder sát nhau từ mép trái dải kem; có thể trượt tới vị trí mới.</summary>
        private void LayoutCards(bool animate)
        {
            List<RectTransform> rects = new List<RectTransform>();
            List<Vector2> targets = new List<Vector2>();
            float x = -StripWidth * 0.5f + StripPadding;
            for (int k = 0; k < _cardOrder.Count; k++)
            {
                WordCard card = _cards[_cardOrder[k]];
                float cw = card.Rect.sizeDelta.x;
                rects.Add(card.Rect);
                targets.Add(new Vector2(x + cw * 0.5f, 0f));
                x += cw + CardGap;
            }

            _layoutVersion++;
            if (!animate)
            {
                for (int k = 0; k < rects.Count; k++) rects[k].anchoredPosition = targets[k];
                return;
            }
            StartCoroutine(SlideCards(rects, targets, _layoutVersion));
        }

        private IEnumerator SlideCards(List<RectTransform> rects, List<Vector2> targets, int version)
        {
            List<Vector2> starts = new List<Vector2>();
            for (int k = 0; k < rects.Count; k++) starts.Add(rects[k].anchoredPosition);

            const float duration = 0.2f;
            float time = 0f;
            while (time < duration)
            {
                if (version != _layoutVersion) yield break;
                time += Time.deltaTime;
                float t = PATween.OutCubic(Mathf.Clamp01(time / duration));
                for (int k = 0; k < rects.Count; k++)
                {
                    if (rects[k] != null) rects[k].anchoredPosition = Vector2.LerpUnclamped(starts[k], targets[k], t);
                }
                yield return null;
            }
            if (version != _layoutVersion) yield break;
            for (int k = 0; k < rects.Count; k++)
            {
                if (rects[k] != null) rects[k].anchoredPosition = targets[k];
            }
        }

        /// <summary>
        /// Trên web, Luna không cắt UI theo RectMask2D (cả Image lẫn chữ TextMeshPro vẫn lộ ra ngoài khung).
        /// Tự ẩn cả ô và chữ của ô không nằm trọn trong dải kem.
        /// </summary>
        private void UpdateCardClipping()
        {
            if (_cards == null || _cardStrip == null) return;
            float left = -StripWidth * 0.5f;
            float right = StripWidth * 0.5f;
            for (int w = 0; w < _cards.Length; w++)
            {
                WordCard card = _cards[w];
                if (card == null || !card.Rect.gameObject.activeSelf) continue;
                float cardX = card.Rect.anchoredPosition.x;
                float scale = card.Rect.localScale.x;
                for (int i = 0; i < card.Slots.Length; i++)
                {
                    RectTransform slot = card.Slots[i].rectTransform;
                    float center = cardX + slot.anchoredPosition.x * scale;
                    float half = _slotSize * 0.5f * scale;
                    bool inside = center - half >= left - 0.5f && center + half <= right + 0.5f;
                    if (card.Labels[i].enabled != inside) card.Labels[i].enabled = inside;
                    if (card.Slots[i].enabled != inside) card.Slots[i].enabled = inside;
                }
            }
        }

        /// <summary>Đưa thẻ của từ vừa tìm lên vị trí đầu (nếu nó đang ở sau/bị che) để chữ bay vào chỗ nhìn thấy.</summary>
        private void BringCardToFront(int wordIndex)
        {
            int pos = _cardOrder.IndexOf(wordIndex);
            if (pos <= 0) return;
            _cardOrder.RemoveAt(pos);
            _cardOrder.Insert(0, wordIndex);
            LayoutCards(true);
        }

        /// <summary>Thẻ đã clear: phồng nhẹ rồi thu nhỏ + mờ dần, sau đó các thẻ phía sau trượt lên lấp chỗ.</summary>
        private IEnumerator RemoveCard(int wordIndex)
        {
            WordCard card = _cards[wordIndex];
            yield return new WaitForSeconds(0.25f);

            const float duration = 0.3f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                float s = t < 0.3f ? Mathf.Lerp(1f, 1.12f, t / 0.3f) : Mathf.Lerp(1.12f, 0f, PATween.InCubic((t - 0.3f) / 0.7f));
                card.Rect.localScale = new Vector3(s, s, 1f);
                card.Group.alpha = 1f - PATween.InCubic(t);
                yield return null;
            }
            card.Rect.gameObject.SetActive(false);

            _cardOrder.Remove(wordIndex);
            LayoutCards(true);
        }


        private void BuildBoard()
        {
            // Board nằm ngay dưới vùng thẻ từ (vị trí phụ thuộc số hàng thẻ).
            float cardsBottom = _header.anchoredPosition.y - _header.sizeDelta.y * 0.5f - _cardsHeight;
            float boardTop = cardsBottom - 18f;
            float boardHeight = CellH * GridSize;
            _board = NewRect("Board", _root);
            _board.sizeDelta = new Vector2(CellW * GridSize, boardHeight);
            _board.anchoredPosition = new Vector2(0f, boardTop - boardHeight * 0.5f);
            _messageY = boardTop - boardHeight - 70f;

            // Sinh board cố định (seed cố định): chữ nhiễu ở mọi ô, rồi rải chữ của từng từ vào các ô ngẫu nhiên,
            // ưu tiên không kề nhau với chữ cùng từ để từ không bị "lộ" thành một hàng.
            PARandom rng = new PARandom(BoardSeed);
            char[,] letters = new char[GridSize, GridSize];
            bool[,] occupied = new bool[GridSize, GridSize];
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++) letters[r, c] = _filler[rng.Range(0, _filler.Length)];
            }

            int[][] cellsOf = new int[_words.Length][];
            for (int w = 0; w < _words.Length; w++)
            {
                string word = _words[w];
                cellsOf[w] = new int[word.Length];
                for (int i = 0; i < word.Length; i++)
                {
                    int cell = PickCell(rng, occupied, cellsOf[w], i, w == 0);
                    cellsOf[w][i] = cell;
                    occupied[cell / GridSize, cell % GridSize] = true;
                    letters[cell / GridSize, cell % GridSize] = word[i];
                }
            }

            _tiles = new LetterTile[GridSize, GridSize];
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    _tiles[r, c] = CreateTile(letters[r, c], r, c, CellPosition(r, c));
                }
            }

            _wordTiles = new LetterTile[_words.Length][];
            for (int w = 0; w < _words.Length; w++)
            {
                _wordTiles[w] = new LetterTile[_words[w].Length];
                for (int i = 0; i < _words[w].Length; i++)
                {
                    int cell = cellsOf[w][i];
                    _wordTiles[w][i] = _tiles[cell / GridSize, cell % GridSize];
                }
            }
        }

        /// <summary>
        /// Chọn một ô trống cho chữ thứ <paramref name="count"/> của từ. Ưu tiên ô không kề (8 hướng) với các chữ
        /// đã đặt của cùng từ; từ tutorial tránh viền ngoài để bàn tay chỉ dễ nhìn.
        /// </summary>
        private static int PickCell(PARandom rng, bool[,] occupied, int[] placed, int count, bool inner)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                int r = rng.Range(0, GridSize);
                int c = rng.Range(0, GridSize);
                if (occupied[r, c]) continue;
                if (inner && attempt < 150 && (r == 0 || c == 0 || r == GridSize - 1 || c == GridSize - 1)) continue;

                bool adjacent = false;
                for (int k = 0; k < count && attempt < 120; k++)
                {
                    int pr = placed[k] / GridSize;
                    int pc = placed[k] % GridSize;
                    if (Mathf.Abs(pr - r) <= 1 && Mathf.Abs(pc - c) <= 1) adjacent = true;
                }
                if (adjacent) continue;
                return r * GridSize + c;
            }

            for (int cell = 0; cell < GridSize * GridSize; cell++)
            {
                if (!occupied[cell / GridSize, cell % GridSize]) return cell;
            }
            return 0;
        }

        private static Vector2 CellPosition(int row, int col)
        {
            return new Vector2((col - (GridSize - 1) * 0.5f) * CellW, ((GridSize - 1) * 0.5f - row) * CellH);
        }

        private LetterTile CreateTile(char letter, int row, int col, Vector2 pos)
        {
            Image bg = NewImage("Tile_" + row + "_" + col, _board, tileSprite, new Vector2(TileW, TileH), pos);
            bg.raycastTarget = true;
            TextMeshProUGUI label = NewText("Letter", bg.transform, letter.ToString(), 60f, LetterTile.LetterColor, new Vector2(TileW, TileH), new Vector2(0f, 6f));

            LetterTile tile = bg.gameObject.AddComponent<LetterTile>();
            tile.Letter = letter;
            tile.Row = row;
            tile.Col = col;
            tile.Rect = bg.rectTransform;
            tile.Background = bg;
            tile.Label = label;
            tile.Tapped = OnTileTapped;
            return tile;
        }

        private void BuildTopLayers()
        {
            RectTransform overlayRect = NewRect("TutorialOverlay", _canvasRect);
            Stretch(overlayRect);
            Image dark = overlayRect.gameObject.AddComponent<Image>();
            dark.color = new Color(0f, 0f, 0f, 0.72f);
            dark.raycastTarget = true;
            _overlay = overlayRect.gameObject.AddComponent<CanvasGroup>();
            _overlay.alpha = 0f;
            overlayRect.gameObject.SetActive(false);

            _rootTop = NewRect("RootTop", _canvasRect);
            _rootTop.sizeDelta = new Vector2(RefWidth, RefHeight);

            _focusLayer = NewRect("FocusLayer", _rootTop);
            _fxLayer = NewRect("FxLayer", _rootTop);

            Image pill = NewImage("Message", _rootTop, null, new Vector2(820f, 96f), new Vector2(0f, _messageY));
            pill.color = new Color(0.25f, 0.12f, 0.04f, 0.65f);
            _messageGroup = pill.gameObject.AddComponent<CanvasGroup>();
            _messageGroup.blocksRaycasts = false;
            _message = NewText("Text", pill.transform, "", 46f, Color.white, new Vector2(800f, 90f), new Vector2(0f, 2f));

            Image hand = NewImage("Hand", _rootTop, handSprite, new Vector2(150f, 150f * 173f / 181f), Vector2.zero);
            _hand = hand.rectTransform;
            _hand.pivot = new Vector2(0.1f, 0.92f);
            _hand.gameObject.SetActive(false);
        }

        private void ShowMessage(string text)
        {
            _message.text = text;
            StartCoroutine(MessageRoutine());
        }

        private IEnumerator MessageRoutine()
        {
            RectTransform rt = (RectTransform)_messageGroup.transform;
            _messageGroup.alpha = 0f;
            rt.localScale = new Vector3(0.8f, 0.8f, 1f);
            yield return StartCoroutine(PATween.Run(0.25f, PATween.OutBack, delegate (float t)
            {
                _messageGroup.alpha = Mathf.Clamp01(t);
                float s = Mathf.LerpUnclamped(0.8f, 1f, t);
                rt.localScale = new Vector3(s, s, 1f);
            }));
        }

        // ================================================================ layout
        private void UpdateLayout()
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : RefWidth / RefHeight;
            // Dọc: khớp chiều ngang; ngang/tablet: khớp chiều dọc để toàn bộ layout dọc vẫn nằm gọn.
            float match = aspect > RefWidth / RefHeight ? 1f : 0f;
            if (!Mathf.Approximately(_scaler.matchWidthOrHeight, match)) _scaler.matchWidthOrHeight = match;

            Vector2 size = _canvasRect.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float cover = Mathf.Max(size.x / RefWidth, size.y / RefHeight);
            _background.sizeDelta = new Vector2(RefWidth * cover, RefHeight * cover);
        }

        // ================================================================ helpers
        // ================================================================ audio
        private void SetupAudio()
        {
            // Pool nhiều AudioSource để mỗi sound có pitch/volume riêng và chồng lên nhau được.
            _sfxSources = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                _sfxSources[i] = src;
            }

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = musicVolume;
            if (music != null)
            {
                _music.clip = music;
                _music.Play();
            }
        }

        private IEnumerator FadeMusic(float to, float duration)
        {
            if (_music == null) yield break;
            float from = _music.volume;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                _music.volume = Mathf.Lerp(from, to, Mathf.Clamp01(time / duration));
                yield return null;
            }
            _music.volume = to;
        }

        private void Play(AudioClip clip)
        {
            Play(clip, 1f, 1f);
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _sfxSources == null) return;
            AudioSource src = _sfxSources[_nextSfx];
            _nextSfx = (_nextSfx + 1) % SfxVoices;
            src.Stop();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos)
        {
            RectTransform rt = NewRect(name, parent);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, string text, float fontSize, Color color, Vector2 size, Vector2 pos)
        {
            RectTransform rt = NewRect(name, parent);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
