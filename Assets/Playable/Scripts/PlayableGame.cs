using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PA.TileWord
{
    /// <summary>
    /// Playable Tile Word Explorer v2.
    /// Header hiển thị chủ đề + dòng từ (PEAR LIME MELON MANGO), dài quá thì xuống dòng.
    /// - Chọn chữ có trong bất kỳ từ nào chưa đủ (không cần thứ tự): ô chữ bay về dòng từ kèm vệt sáng.
    /// - Chọn sai: màn hình nháy đỏ, hiện dấu X đỏ, ô chữ rơi vào khay (7 ô).
    /// - Mỗi từ hoàn thành sáng 1 ngôi sao trên thanh tiến trình. Đủ sao -> hỏi chơi cấp khó hơn.
    /// - Khay đầy 7 ô (hoặc hết giờ) -> hỏi thử lại.
    /// - Thanh dưới cùng (logo + tên app + PLAY NOW) hiển thị suốt từ đầu đến cuối.
    /// Toàn bộ UI dựng bằng code (uGUI + TextMeshPro), chỉ dùng API Unity mà Luna Playworks hỗ trợ.
    /// </summary>
    public class PlayableGame : MonoBehaviour
    {
        [Header("Sprites (asset game gốc)")]
        public Sprite backgroundSprite;
        public Sprite tileSprite;
        public Sprite slotSprite;
        public Sprite headerTopSprite;     // StageInfoPanel tím: phần trên (tab level/timer, dải chủ đề, nửa trên dải kem)
        public Sprite headerMidSprite;     // dải 1px giữa dải kem, kéo dài khi dòng từ xuống nhiều dòng
        public Sprite headerBottomSprite;  // phần dưới dải kem + viền đáy
        public Sprite logoSprite;
        public Sprite buttonSprite;
        public Sprite hardButtonSprite;
        public Sprite handSprite;
        public Sprite sparkleSprite;
        public Sprite starOnSprite;
        public Sprite starOffSprite;
        public Sprite crossSprite;       // cross.png (trắng, tô đỏ trong code)
        public Sprite pillCapSprite;     // mép trái 48x128 cắt từ TrayWarningBar (ô bo góc trắng) để ghép nền bo góc
        public Sprite flashEdgesSprite;  // TrayWarningEdges: viền màn hình khi chọn sai
        public Sprite trayWarningSprite;
        public Sprite winPanelSprite;
        public Sprite losePanelSprite;
        public Sprite loseIconSprite;

        [Header("Font")]
        public TMP_FontAsset font;

        [Header("Audio")]
        public AudioClip music;          // music_main: nhạc nền lặp
        public AudioClip sfxTap;         // tile click: chọn đúng chữ
        public AudioClip sfxWrong;       // tile click blocked: chọn sai
        public AudioClip sfxWoodLand;    // crate crack: mảnh gỗ chạm chỗ / rơi vào khay
        public AudioClip sfxHeaderDrop;  // rubber quick: header trượt xuống
        public AudioClip sfxStreak;      // power up: ngôi sao sáng
        public AudioClip sfxSlotFill;    // coin pickup: chữ bay vào dòng từ
        public AudioClip sfxWord;        // coins reward: hoàn thành 1 từ
        public AudioClip sfxWin;         // level complete: popup kết thúc
        public AudioClip sfxButton;      // ui_button_1: bấm CTA

        [Range(0f, 1f)] public float musicVolume = 0.45f;

        [Header("Tuning")]
        public float introDuration = 1.4f;
        [Tooltip("Sau từ đầu tiên, bàn tay gợi ý chỉ hiện lại khi người chơi không tương tác trong khoảng này (giây).")]
        public float idleHintDelay = 5f;

        [Header("Luna Playground")]
        [LunaPlaygroundField("Category title", 0, "Words")]
        public string categoryTitle = "FRUITS";

        [LunaPlaygroundField("Words (in order)", 1, "Words")]
        [LunaPlaygroundFieldArrayLength(1, MaxWords)]
        [Tooltip("Các từ hiện trên header (tối đa 5, mỗi từ 2–8 chữ A–Z). Chữ có trong từ nào cũng được, không cần thứ tự.")]
        public string[] words = { "PEAR", "LIME", "MELON", "MANGO" };

        [LunaPlaygroundField("Words to win (stars)", 0, "Gameplay")]
        [LunaPlaygroundFieldStep(1)]
        [Range(1, MaxWords)]
        [Tooltip("Số từ cần hoàn thành (từ nào cũng được) = số sao trên thanh tiến trình.")]
        public int wordsToWin = 3;

        [LunaPlaygroundField("Level number", 1, "Gameplay")]
        [LunaPlaygroundFieldStep(1)]
        public int levelNumber = 20;

        [LunaPlaygroundField("Time limit (seconds)", 2, "Gameplay")]
        [LunaPlaygroundFieldStep(1)]
        [Range(20, 600)]
        public int timeLimit = 180;

        [LunaPlaygroundField("Grid columns", 3, "Gameplay")]
        [LunaPlaygroundFieldStep(1)]
        [Range(MinGrid, MaxGrid)]
        [Tooltip("Số cột của bảng chữ. Board tự thu nhỏ cho vừa màn hình.")]
        public int gridColumns = 7;

        [LunaPlaygroundField("Grid rows", 4, "Gameplay")]
        [LunaPlaygroundFieldStep(1)]
        [Range(MinGrid, MaxGrid)]
        [Tooltip("Số hàng của bảng chữ. Board tự thu nhỏ cho vừa màn hình.")]
        public int gridRows = 6;

        [LunaPlaygroundField("Tray slots (wrong letters)", 5, "Gameplay")]
        [LunaPlaygroundFieldStep(1)]
        [Range(MinTray, MaxTray)]
        [Tooltip("Số ô khay chứa chữ chọn sai. Khay đầy thì thua (hỏi thử lại).")]
        public int traySlots = 7;

        // ---------------------------------------------------------------- layout constants
        private const float RefWidth = 1080f;
        private const float RefHeight = 1920f;

        private const int MinGrid = 4;
        private const int MaxGrid = 9;
        private const int MinTray = 3;
        private const int MaxTray = 10;
        // Bề rộng tối đa của board / khay trên màn hình 1080 (thừa thì thu nhỏ).
        private const float MaxRowWidth = 1040f;

        private const float CellW = 138f;
        private const float CellH = 142f;
        private const float TileW = 128f;
        private const float TileH = 139f;
        private const float TileFont = 84f;

        private const float TraySlot = 122f;
        private const float TrayGap = 12f;

        private const float HeaderW = 1000f;
        private const float HeaderH = HeaderW * 414f / 1080f;
        private const float HeaderScale = HeaderW / 1080f;
        private const float HeaderTop = 810f;
        // Ảnh header 1080x414 cắt ở hàng 334 (giữa dải kem 290..378).
        private const float HeaderSplitPx = 334f;
        // Đáy board thấp nhất để ô thông báo không chạm thanh CTA dưới cùng.
        private const float MinBoardBottom = -655f;
        private const float StarBarY = 880f;
        private const float StarSpacing = 112f;
        private const float BottomBarHeight = 190f;

        private const float LineFont = 62f;
        private const float LineMaxWidth = 860f;
        private const float LineSpacing = LineFont * 1.12f;
        // Thu nhỏ dòng từ tối đa tới mức này trước khi xuống dòng.
        private const float MinLineScale = 0.9f;
        private const float LineTracking = 3f;

        private const int MaxWords = 5;
        private const int MinWordLength = 2;
        private const int MaxWordLength = 8;
        private const int BoardSeed = 20261125;
        private const string ExtraDecoys = "BCDGHKSTUY";
        private static readonly string[] DefaultWords = { "PEAR", "LIME", "MELON", "MANGO" };

        // Độ rộng chữ in hoa Nunito Black (em = 100), đo từ file TTF: dàn dòng từ không phụ thuộc TMP layout.
        private static readonly float[] LetterAdvance =
        {
            76f, 70f, 69f, 79f, 61f, 57f, 75f, 78f, 30f, 39f, 71f, 58f, 89f,
            76f, 82f, 67f, 82f, 70f, 65f, 65f, 75f, 74f, 113f, 70f, 65f, 63f,
        };

        private static readonly Color Brown = new Color32(107, 58, 30, 255);
        private static readonly Color TimerWarnColor = new Color32(214, 40, 30, 255);
        private static readonly Color PendingColor = new Color(0.42f, 0.23f, 0.12f, 0.28f);
        private static readonly Color IncomingColor = new Color(0.42f, 0.23f, 0.12f, 0.5f);
        private static readonly Color WordDoneColor = new Color32(58, 150, 40, 255);
        private static readonly Color StreakColor = new Color(1f, 0.93f, 0.55f, 0.95f);
        private static readonly Color TrailColor = new Color(1f, 0.97f, 0.75f, 0.85f);
        private static readonly Color OutlinePurple = new Color32(70, 14, 105, 255);

        private enum Phase { Intro, Tutorial, Free, End }

        // ---------------------------------------------------------------- runtime
        private string[] _words;
        private int _target;
        private string _decoys;

        private RectTransform _canvasRect;
        private CanvasScaler _scaler;
        private RectTransform _background;
        private RectTransform _root;
        private RectTransform _rootTop;
        private RectTransform _header;
        private RectTransform _board;
        private RectTransform _trayLayer;
        private RectTransform _starBar;
        private RectTransform _fxLayer;
        private RectTransform _hand;
        private RectTransform _bottomBar;
        private RectTransform _ctaButton;
        private Image _flash;
        private Image _flashEdges;
        private CanvasGroup _messageGroup;
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _timerText;

        private RectTransform _wordLine;
        private float _lineScale = 1f;
        private List<List<int>> _lines;
        private float _headerExtra;
        private float _trayY;
        private float _boardTop;
        private float _boardScale = 1f;
        private float _messageY;
        private int _cols;
        private int _rows;
        private int _traySize;
        private float _trayScale = 1f;
        private TextMeshProUGUI[][] _lineLabels;

        private Image[] _traySlots;
        private Image[] _trayWarnings;
        private Image[] _stars;

        private const int SfxVoices = 8;
        private AudioSource[] _sfxSources;
        private int _nextSfx;
        private AudioSource _music;
        private float _lastLandSfx = -1f;

        private LetterTile[,] _tiles;

        private Phase _phase = Phase.Intro;
        private bool[][] _slotTaken;
        private int[] _assigned;
        private int _completedWords;
        private int _starsLit;
        private int[] _landed;
        private int _foundCount;
        private int _trayCount;
        private int _flyingLetters;
        private bool _lost;
        private bool _timeUp;
        private float _timeLeft;
        private int _shownSeconds = -1;
        private float _idleTime;
        private RectTransform _handTarget;
        private Coroutine _handLoop;
        private int _flashVersion;
        private bool _storeOpened;

        // ================================================================ lifecycle
        private void Start()
        {
            Application.targetFrameRate = 60;
            ReadGridConfig();
            PrepareWords();
            PrepareLineLayout();
            ComputeLayout();
            SetupAudio();

            EnsureEventSystem();
            BuildCanvas();
            BuildBoard();
            BuildTray();
            BuildHeader();
            BuildStarBar();
            BuildTopLayers();
            BuildBottomBar();
            UpdateLayout();

            StartCoroutine(Flow());
        }

        private void Update()
        {
            UpdateLayout();

            if (_phase == Phase.Tutorial || _phase == Phase.Free)
            {
                _timeLeft -= Time.deltaTime;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    _timeUp = true;
                    Lose();
                }
                UpdateTimerText();
            }

            if (_phase == Phase.Free && _handTarget == null)
            {
                _idleTime += Time.deltaTime;
                if (_idleTime >= idleHintDelay)
                {
                    LetterTile next = NextHintTile();
                    if (next != null) ShowHand(next.Rect);
                }
            }

            float now = Time.time;
            if (_trayWarnings != null)
            {
                bool warn = _trayCount >= Mathf.Max(1, _traySize - 2);
                float a = 0.55f + Mathf.Sin(now * 8f) * 0.45f;
                for (int i = 0; i < _trayWarnings.Length; i++)
                {
                    if (_trayWarnings[i].enabled != warn) _trayWarnings[i].enabled = warn;
                    if (warn) _trayWarnings[i].color = new Color(1f, 1f, 1f, a);
                }
            }

            if (_ctaButton != null)
            {
                float s = 1f + Mathf.Sin(now * 5f) * 0.05f;
                _ctaButton.localScale = new Vector3(s, s, 1f);
            }
        }

        /// <summary>Kích thước board và khay từ Luna Playground, kẹp trong khoảng hợp lệ.</summary>
        private void ReadGridConfig()
        {
            _cols = Mathf.Clamp(gridColumns, MinGrid, MaxGrid);
            _rows = Mathf.Clamp(gridRows, MinGrid, MaxGrid);
            _traySize = Mathf.Clamp(traySlots, MinTray, MaxTray);
        }

        // ================================================================ words config
        /// <summary>Chuẩn hoá danh sách từ từ Luna: in hoa, chỉ giữ A–Z, bỏ từ quá ngắn/dài, tối đa 5 từ.</summary>
        private void PrepareWords()
        {
            List<string> list = new List<string>();
            if (words != null)
            {
                for (int i = 0; i < words.Length && list.Count < MaxWords; i++)
                {
                    string clean = CleanWord(words[i]);
                    if (clean.Length < MinWordLength || clean.Length > MaxWordLength) continue;
                    if (list.Contains(clean)) continue;
                    list.Add(clean);
                }
            }
            if (list.Count == 0)
            {
                Debug.LogWarning("[Playable] Danh sách từ trống/không hợp lệ, dùng mặc định.");
                for (int i = 0; i < DefaultWords.Length; i++) list.Add(DefaultWords[i]);
            }
            _words = list.ToArray();
            _target = Mathf.Clamp(wordsToWin, 1, _words.Length);
            _landed = new int[_words.Length];
            _assigned = new int[_words.Length];
            _slotTaken = new bool[_words.Length][];
            for (int w = 0; w < _words.Length; w++) _slotTaken[w] = new bool[_words[w].Length];

            // Chữ lấp board: phần lớn là chữ có trong các từ (dễ nhầm thứ tự) + vài chữ nhiễu khác.
            _decoys = ExtraDecoys;
            for (int w = 0; w < _words.Length; w++) _decoys += _words[w];
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

        /// <summary>
        /// Ô còn trống cho chữ <paramref name="ch"/>: ưu tiên từ gần xong nhất (ít ô trống nhất), hoà thì từ đứng trước.
        /// Không cần đúng thứ tự: chữ có trong bất kỳ từ nào chưa đủ đều hợp lệ.
        /// </summary>
        private bool FindSlot(char ch, out int word, out int index)
        {
            word = -1;
            index = -1;
            int bestRemaining = int.MaxValue;
            for (int w = 0; w < _words.Length; w++)
            {
                int remaining = _words[w].Length - _assigned[w];
                if (remaining <= 0 || remaining >= bestRemaining) continue;
                for (int i = 0; i < _words[w].Length; i++)
                {
                    if (_slotTaken[w][i] || _words[w][i] != ch) continue;
                    word = w;
                    index = i;
                    bestRemaining = remaining;
                    break;
                }
            }
            return word >= 0;
        }

        /// <summary>Từ nên gợi ý: từ đầu tiên trong tutorial, sau đó là từ gần xong nhất.</summary>
        private int FocusWord()
        {
            if (_phase == Phase.Tutorial && _assigned[0] < _words[0].Length) return 0;
            int best = -1;
            int bestRemaining = int.MaxValue;
            for (int w = 0; w < _words.Length; w++)
            {
                int remaining = _words[w].Length - _assigned[w];
                if (remaining > 0 && remaining < bestRemaining)
                {
                    best = w;
                    bestRemaining = remaining;
                }
            }
            return best;
        }

        /// <summary>Các chữ còn thiếu của một từ (theo thứ tự trong từ).</summary>
        private string MissingLetters(int w)
        {
            string s = "";
            if (w < 0) return s;
            for (int i = 0; i < _words[w].Length; i++)
            {
                if (!_slotTaken[w][i]) s += _words[w][i];
            }
            return s;
        }

        /// <summary>Tất cả chữ còn thiếu của mọi từ.</summary>
        private string AllMissingLetters()
        {
            string s = "";
            for (int w = 0; w < _words.Length; w++) s += MissingLetters(w);
            return s;
        }

        // ================================================================ flow
        private IEnumerator Flow()
        {
            yield return StartCoroutine(Intro());

            _phase = Phase.Tutorial;
            _timeLeft = timeLimit;
            ShowMessage("Tap letters to fill the words!");
            RefreshTutorialHand();

            while (_phase != Phase.End) yield return null;
            HideHand();
            HideMessage();
            while (_flyingLetters > 0) yield return null;
            if (!_lost)
            {
                while (_foundCount < _target) yield return null;
            }

            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(EndPopup(!_lost));
        }

        private IEnumerator Intro()
        {
            _phase = Phase.Intro;
            _messageGroup.alpha = 0f;

            // Ô gỗ rơi lần lượt kiểu "rắn": hàng dưới trái -> phải, hàng trên phải -> trái...
            const float fallTime = 0.4f;
            int maxOrder = _rows * _cols - 1;
            float step = Mathf.Max(0.01f, (introDuration - fallTime) / maxOrder);
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    LetterTile tile = _tiles[r, c];
                    int rowFromBottom = _rows - 1 - r;
                    int xOrder = rowFromBottom % 2 == 0 ? c : _cols - 1 - c;
                    Vector2 to = CellPosition(r, c);
                    tile.FallTo(to + new Vector2(0f, 1500f), to, (rowFromBottom * _cols + xOrder) * step, fallTime);
                }
            }

            _trayLayer.localScale = Vector3.zero;
            _starBar.localScale = Vector3.zero;
            Vector2 headerHome = _header.anchoredPosition;
            Vector2 headerFrom = headerHome + new Vector2(0f, 700f);
            _header.anchoredPosition = headerFrom;

            float time = 0f;
            bool headerSound = false;
            bool trayShown = false;
            float end = introDuration + 0.35f;
            while (time < end)
            {
                time += Time.deltaTime;
                float startH = introDuration * 0.3f;
                if (!headerSound && time >= startH)
                {
                    headerSound = true;
                    Play(sfxHeaderDrop, 0.8f, 1f);
                }
                float h = Mathf.Clamp01((time - startH) / 0.5f);
                _header.anchoredPosition = Vector2.LerpUnclamped(headerFrom, headerHome, PATween.OutBack(h));

                if (!trayShown && time >= introDuration * 0.6f)
                {
                    trayShown = true;
                    StartCoroutine(PATween.Scale(_trayLayer, Vector3.zero, new Vector3(_trayScale, _trayScale, 1f), 0.35f, PATween.OutBack));
                    StartCoroutine(PATween.Scale(_starBar, Vector3.zero, Vector3.one, 0.35f, PATween.OutBack));
                }
                yield return null;
            }
            _header.anchoredPosition = headerHome;
        }

        // ================================================================ input
        private void OnTileTapped(LetterTile tile)
        {
            if (_phase != Phase.Tutorial && _phase != Phase.Free) return;
            if (tile.Used) return;

            _idleTime = 0f;
            HideHand();

            tile.StopMove();
            tile.Used = true;
            if (_tiles[tile.Row, tile.Col] == tile) _tiles[tile.Row, tile.Col] = null;
            tile.Rect.SetParent(_fxLayer, true);
            tile.Rect.SetAsLastSibling();

            int w;
            int i;
            if (FindSlot(tile.Letter, out w, out i)) OnCorrect(tile, w, i);
            else OnWrong(tile);

            RefillBoard();
            RefreshTutorialHand();
        }

        private void OnCorrect(LetterTile tile, int w, int i)
        {
            _slotTaken[w][i] = true;
            _assigned[w]++;
            Play(sfxTap, 1f, 1f + 0.08f * (_assigned[w] - 1));
            SetLabelColor(_lineLabels[w][i], IncomingColor);

            if (_assigned[w] >= _words[w].Length)
            {
                _completedWords++;
                if (_completedWords >= _target)
                {
                    _phase = Phase.End;
                }
                else if (_phase == Phase.Tutorial)
                {
                    _phase = Phase.Free;
                    int remaining = _target - _completedWords;
                    ShowMessage("Great! " + remaining + (remaining == 1 ? " more word!" : " more words!"));
                }
            }

            _flyingLetters++;
            StartCoroutine(FlyToWord(tile, w, i));
        }

        private void OnWrong(LetterTile tile)
        {
            int slot = _trayCount;
            _trayCount++;
            Play(sfxWrong, 1f, 1f);
            StartCoroutine(FlashRed(++_flashVersion));
            StartCoroutine(CrossRoutine(tile.Rect.anchoredPosition));

            if (_trayCount >= _traySize) Lose();

            _flyingLetters++;
            StartCoroutine(FallToTray(tile, slot));
        }

        private void Lose()
        {
            if (_phase == Phase.End) return;
            _lost = true;
            _phase = Phase.End;
        }

        private void RefreshTutorialHand()
        {
            if (_phase != Phase.Tutorial) return;
            LetterTile next = NextHintTile();
            if (next != null) ShowHand(next.Rect);
        }

        /// <summary>Ô gợi ý: chữ còn thiếu đầu tiên của từ đang tập trung mà có trên board, ưu tiên ô gần giữa board.</summary>
        private LetterTile NextHintTile()
        {
            if (_phase == Phase.End) return null;
            string missing = MissingLetters(FocusWord());
            for (int k = 0; k < missing.Length; k++)
            {
                LetterTile t = ClosestTile(missing[k]);
                if (t != null) return t;
            }
            string all = AllMissingLetters();
            for (int k = 0; k < all.Length; k++)
            {
                LetterTile t = ClosestTile(all[k]);
                if (t != null) return t;
            }
            return null;
        }

        private LetterTile ClosestTile(char need)
        {
            LetterTile best = null;
            float bestDist = float.MaxValue;
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    LetterTile t = _tiles[r, c];
                    if (t == null || t.Used || t.Letter != need) continue;
                    float d = Mathf.Abs(r - (_rows - 1) * 0.5f) + Mathf.Abs(c - (_cols - 1) * 0.5f);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = t;
                    }
                }
            }
            return best;
        }

        // ================================================================ board
        private void BuildBoard()
        {
            _board = NewRect("Board", _root);
            _board.sizeDelta = new Vector2(CellW * _cols, CellH * _rows);
            _board.localScale = new Vector3(_boardScale, _boardScale, 1f);
            _board.anchoredPosition = new Vector2(0f, _boardTop - CellH * _rows * _boardScale * 0.5f);

            // Board cố định (seed cố định): chữ nhiễu ở mọi ô, rồi đặt đủ chữ của 2 từ đầu vào các ô khác nhau.
            PARandom rng = new PARandom(BoardSeed);
            char[,] letters = new char[_rows, _cols];
            bool[,] reserved = new bool[_rows, _cols];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++) letters[r, c] = _decoys[rng.Range(0, _decoys.Length)];
            }
            int seeded = Mathf.Min(2, _target);
            for (int w = 0; w < seeded; w++)
            {
                string word = _words[w];
                for (int i = 0; i < word.Length; i++)
                {
                    for (int attempt = 0; attempt < 300; attempt++)
                    {
                        int r = rng.Range(0, _rows);
                        int c = rng.Range(0, _cols);
                        if (reserved[r, c]) continue;
                        // Từ tutorial tránh viền ngoài để bàn tay chỉ dễ nhìn.
                        if (w == 0 && attempt < 200 && (r == 0 || c == 0 || r == _rows - 1 || c == _cols - 1)) continue;
                        reserved[r, c] = true;
                        letters[r, c] = word[i];
                        break;
                    }
                }
            }

            _tiles = new LetterTile[_rows, _cols];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    _tiles[r, c] = CreateTile(letters[r, c], r, c, CellPosition(r, c));
                }
            }
        }

        private Vector2 CellPosition(int row, int col)
        {
            return new Vector2((col - (_cols - 1) * 0.5f) * CellW, ((_rows - 1) * 0.5f - row) * CellH);
        }

        private LetterTile CreateTile(char letter, int row, int col, Vector2 pos)
        {
            Image bg = NewImage("Tile_" + row + "_" + col, _board, tileSprite, new Vector2(TileW, TileH), pos);
            bg.raycastTarget = true;
            TextMeshProUGUI label = NewText("Letter", bg.transform, letter.ToString(), TileFont, LetterTile.LetterColor, new Vector2(TileW, TileH), new Vector2(0f, 7f));

            LetterTile tile = bg.gameObject.AddComponent<LetterTile>();
            tile.Letter = letter;
            tile.Row = row;
            tile.Col = col;
            tile.Rect = bg.rectTransform;
            tile.Background = bg;
            tile.Label = label;
            tile.Tapped = OnTileTapped;
            tile.Landed = OnTileLanded;
            return tile;
        }

        private void OnTileLanded(LetterTile tile)
        {
            // Tối đa 1 tiếng gỗ mỗi 0.07s để không bị dồn tiếng khi cả cột rơi.
            if (Time.time - _lastLandSfx < 0.07f) return;
            _lastLandSfx = Time.time;
            Play(sfxWoodLand, 0.4f, Random.Range(0.92f, 1.15f));
        }

        /// <summary>
        /// Ô phía trên rơi xuống lấp chỗ trống, ô mới hiện ra ở các ô trên cùng.
        /// Mỗi ô tự quản lý chuyển động nên người chơi vẫn chạm được trong lúc board đang rơi.
        /// </summary>
        private void RefillBoard()
        {
            List<LetterTile> spawned = new List<LetterTile>();
            for (int c = 0; c < _cols; c++)
            {
                int write = _rows - 1;
                for (int r = _rows - 1; r >= 0; r--)
                {
                    LetterTile tile = _tiles[r, c];
                    if (tile == null) continue;
                    if (r != write)
                    {
                        _tiles[write, c] = tile;
                        _tiles[r, c] = null;
                        tile.Row = write;
                        tile.FallTo(tile.Rect.anchoredPosition, CellPosition(write, c), 0f, 0.3f);
                    }
                    write--;
                }

                for (int r = write; r >= 0; r--)
                {
                    LetterTile tile = CreateTile(NextFillLetter(), r, c, CellPosition(r, c));
                    tile.PopIn(0.12f + (write - r) * 0.05f);
                    _tiles[r, c] = tile;
                    spawned.Add(tile);
                }
            }
            EnsureUpcomingLetters(spawned);
        }

        private char NextFillLetter()
        {
            string missing = AllMissingLetters();
            if (missing.Length > 0 && Random.value < 0.3f) return missing[Random.Range(0, missing.Length)];
            return _decoys[Random.Range(0, _decoys.Length)];
        }

        /// <summary>
        /// Luôn có ô cho 2 chữ còn thiếu của từ đang tập trung. Thiếu thì đổi chữ của một ô
        /// (ưu tiên ô vừa xuất hiện, chưa kịp nhìn thấy; không đổi ô đang có chữ hữu ích).
        /// </summary>
        private void EnsureUpcomingLetters(List<LetterTile> preferred)
        {
            if (_phase == Phase.End) return;
            string upcoming = MissingLetters(FocusWord());
            if (upcoming.Length > 2) upcoming = upcoming.Substring(0, 2);
            string keep = AllMissingLetters();
            List<LetterTile> claimed = new List<LetterTile>();
            for (int k = 0; k < upcoming.Length; k++)
            {
                char ch = upcoming[k];
                LetterTile found = null;
                for (int r = 0; r < _rows && found == null; r++)
                {
                    for (int c = 0; c < _cols; c++)
                    {
                        LetterTile t = _tiles[r, c];
                        if (t != null && t.Letter == ch && !claimed.Contains(t))
                        {
                            found = t;
                            break;
                        }
                    }
                }
                if (found == null) found = ConvertTile(ch, keep, claimed, preferred);
                if (found != null) claimed.Add(found);
            }
        }

        private LetterTile ConvertTile(char letter, string keep, List<LetterTile> claimed, List<LetterTile> preferred)
        {
            List<LetterTile> candidates = new List<LetterTile>();
            for (int i = 0; i < preferred.Count; i++)
            {
                LetterTile t = preferred[i];
                if (t != null && !t.Used && !claimed.Contains(t) && keep.IndexOf(t.Letter) < 0) candidates.Add(t);
            }
            if (candidates.Count == 0)
            {
                for (int r = 0; r < _rows; r++)
                {
                    for (int c = 0; c < _cols; c++)
                    {
                        LetterTile t = _tiles[r, c];
                        if (t == null || claimed.Contains(t) || keep.IndexOf(t.Letter) >= 0) continue;
                        if (_handTarget != null && t.Rect == _handTarget) continue;
                        candidates.Add(t);
                    }
                }
            }
            if (candidates.Count == 0) return null;
            LetterTile pick = candidates[Random.Range(0, candidates.Count)];
            pick.Letter = letter;
            pick.Label.text = letter.ToString();
            return pick;
        }

        // ================================================================ correct letter -> word line
        private IEnumerator FlyToWord(LetterTile tile, int w, int i)
        {
            TextMeshProUGUI label = _lineLabels[w][i];
            Vector2 p0 = tile.Rect.anchoredPosition;
            Vector2 p2 = _fxLayer.InverseTransformPoint(label.rectTransform.position);
            Vector2 p1 = (p0 + p2) * 0.5f + new Vector2(p0.x < p2.x ? -170f : 170f, 120f);
            float startScale = tile.Rect.localScale.x;
            float endScale = LineFont * _lineScale / TileFont;
            Color bgColor = tile.Background.color;

            SpawnSparkle(p0, 130f, 0.4f);
            const float duration = 0.6f;
            float time = 0f;
            Vector2 prev = p0;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = PATween.InOutSine(Mathf.Clamp01(time / duration));
                float u = 1f - t;
                Vector2 pos = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                tile.Rect.anchoredPosition = pos;
                float s = Mathf.Lerp(startScale, endScale, t);
                tile.Rect.localScale = new Vector3(s, s, 1f);
                Color c = bgColor;
                c.a = 1f - Mathf.Clamp01((t - 0.45f) / 0.4f);
                tile.Background.color = c;

                // Vệt sáng liền mạch dọc theo đường bay + lấp lánh.
                SpawnTrail(prev, pos, Mathf.Lerp(46f, 26f, t), 0.35f);
                if (Random.value < 0.5f) SpawnSparkle(pos + Random.insideUnitCircle * 20f, Random.Range(50f, 80f), 0.4f);
                prev = pos;
                yield return null;
            }

            Destroy(tile.gameObject);
            SetLabelColor(label, Brown);
            Play(sfxSlotFill, 0.7f, 1f + 0.08f * i);
            SpawnSparkle(p2, 110f, 0.4f);
            StartCoroutine(PATween.Punch(label.rectTransform, 0.45f, 0.25f));

            _landed[w]++;
            if (_landed[w] >= _words[w].Length) StartCoroutine(WordDone(w));
            else _flyingLetters--;
        }

        /// <summary>Từ hoàn thành: đổi màu xanh, một ngôi sao bay lên thanh tiến trình và sáng lên.</summary>
        private IEnumerator WordDone(int w)
        {
            Play(sfxWord);
            TextMeshProUGUI[] labels = _lineLabels[w];
            Vector2 center = Vector2.zero;
            for (int i = 0; i < labels.Length; i++)
            {
                SetLabelColor(labels[i], WordDoneColor);
                Vector2 p = _fxLayer.InverseTransformPoint(labels[i].rectTransform.position);
                center += p;
                SpawnSparkle(p + Random.insideUnitCircle * 25f, Random.Range(60f, 100f), Random.Range(0.4f, 0.7f));
                StartCoroutine(PATween.Punch(labels[i].rectTransform, 0.3f, 0.3f));
            }
            center /= labels.Length;

            // Sao sáng theo thứ tự hoàn thành từ.
            int starIndex = Mathf.Min(_starsLit, _stars.Length - 1);
            _starsLit++;
            Image target = _stars[starIndex];
            Vector2 p2 = _fxLayer.InverseTransformPoint(target.rectTransform.position);
            Image flying = NewImage("FlyingStar", _fxLayer, starOnSprite, new Vector2(90f, 90f), center);
            Vector2 p1 = (center + p2) * 0.5f + new Vector2(220f, 60f);

            const float duration = 0.55f;
            float time = 0f;
            Vector2 prev = center;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = PATween.InOutSine(Mathf.Clamp01(time / duration));
                float u = 1f - t;
                Vector2 pos = u * u * center + 2f * u * t * p1 + t * t * p2;
                flying.rectTransform.anchoredPosition = pos;
                flying.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 360f);
                SpawnTrail(prev, pos, 34f, 0.3f);
                prev = pos;
                yield return null;
            }
            Destroy(flying.gameObject);

            target.sprite = starOnSprite;
            Play(sfxStreak, 0.8f, 1f + 0.1f * starIndex);
            for (int k = 0; k < 6; k++) SpawnSparkle(p2 + Random.insideUnitCircle * 40f, Random.Range(50f, 90f), Random.Range(0.4f, 0.7f));
            StartCoroutine(PATween.Punch(target.rectTransform, 0.5f, 0.35f));

            _foundCount++;
            _flyingLetters--;
        }

        // ================================================================ wrong letter -> tray
        private IEnumerator FallToTray(LetterTile tile, int slotIndex)
        {
            Image slot = _traySlots[slotIndex];
            tile.Background.color = LetterTile.WrongColor;

            // Rung tại chỗ.
            Vector2 basePos = tile.Rect.anchoredPosition;
            float time = 0f;
            const float shake = 0.28f;
            while (time < shake)
            {
                time += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(time / shake);
                tile.Rect.anchoredPosition = basePos + new Vector2(Mathf.Sin(time * 70f) * 14f * k, 0f);
                yield return null;
            }

            // Bay vòng vào ô khay.
            Vector2 p0 = basePos;
            Vector2 p2 = _fxLayer.InverseTransformPoint(slot.rectTransform.position);
            Vector2 p1 = new Vector2((p0.x + p2.x) * 0.5f, Mathf.Max(p0.y, p2.y) + 140f);
            float startScale = tile.Rect.localScale.x;
            float slotScale = TraySlot * 0.94f / TileH;
            float endScale = slotScale * _trayScale;
            const float duration = 0.45f;
            time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = PATween.InOutSine(Mathf.Clamp01(time / duration));
                float u = 1f - t;
                tile.Rect.anchoredPosition = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                float s = Mathf.Lerp(startScale, endScale, t);
                tile.Rect.localScale = new Vector3(s, s, 1f);
                tile.Background.color = Color.Lerp(LetterTile.WrongColor, LetterTile.NormalColor, t);
                yield return null;
            }

            tile.Rect.SetParent(slot.transform, false);
            tile.Rect.anchoredPosition = Vector2.zero;
            tile.Rect.localScale = new Vector3(slotScale, slotScale, 1f);
            tile.Background.color = LetterTile.NormalColor;
            tile.Background.raycastTarget = false;
            Play(sfxWoodLand, 0.8f, 1f);
            StartCoroutine(PATween.Punch(slot.rectTransform, 0.15f, 0.2f));
            _flyingLetters--;
        }

        private IEnumerator FlashRed(int version)
        {
            // Nháy đỏ 2 lần.
            const float duration = 0.5f;
            float time = 0f;
            while (time < duration)
            {
                if (version != _flashVersion) yield break;
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                float k = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * (1f - t * 0.5f);
                _flash.color = new Color(1f, 0.05f, 0.05f, k * 0.22f);
                _flashEdges.color = new Color(1f, 0.08f, 0.05f, k);
                yield return null;
            }
            if (version == _flashVersion)
            {
                _flash.color = new Color(1f, 0.05f, 0.05f, 0f);
                _flashEdges.color = new Color(1f, 0.08f, 0.05f, 0f);
            }
        }

        private IEnumerator CrossRoutine(Vector2 pos)
        {
            // cross.png gốc màu trắng: bản trắng to hơn làm viền, bản đỏ đè lên.
            RectTransform rt = NewRect("WrongX", _fxLayer);
            rt.anchoredPosition = pos;
            CanvasGroup group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            NewImage("Rim", rt, crossSprite, new Vector2(172f, 172f), Vector2.zero);
            Image red = NewImage("Red", rt, crossSprite, new Vector2(140f, 140f), Vector2.zero);
            red.color = new Color32(226, 30, 36, 255);
            float time = 0f;
            const float total = 0.85f;
            while (time < total)
            {
                time += Time.deltaTime;
                float s = time < 0.2f ? PATween.OutBack(time / 0.2f) : 1f;
                rt.localScale = new Vector3(s, s, 1f);
                float a = time < 0.6f ? 1f : 1f - (time - 0.6f) / 0.25f;
                group.alpha = Mathf.Clamp01(a);
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        // ================================================================ fx
        private void SpawnTrail(Vector2 from, Vector2 to, float width, float life)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 0.5f) return;
            RectTransform rt = NewRect("Trail", _fxLayer);
            rt.SetAsFirstSibling();
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = from;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            rt.sizeDelta = new Vector2(len + width * 0.5f, width);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = TrailColor;
            img.raycastTarget = false;
            StartCoroutine(TrailRoutine(img, width, life));
        }

        private IEnumerator TrailRoutine(Image img, float width, float life)
        {
            RectTransform rt = img.rectTransform;
            float time = 0f;
            while (time < life)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / life);
                Color c = TrailColor;
                c.a *= 1f - t;
                img.color = c;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, width * (1f - t * 0.8f));
                yield return null;
            }
            Destroy(img.gameObject);
        }

        private void SpawnSparkle(Vector2 pos, float size, float life)
        {
            Image img = NewImage("Sparkle", _fxLayer, sparkleSprite, new Vector2(size, size), pos);
            img.color = StreakColor;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            StartCoroutine(SparkleRoutine(img, life));
        }

        private IEnumerator SparkleRoutine(Image img, float life)
        {
            Color baseColor = img.color;
            float time = 0f;
            while (time < life)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / life);
                Color c = baseColor;
                c.a = baseColor.a * (1f - t);
                img.color = c;
                float s = Mathf.Lerp(1f, 0.3f, t);
                img.rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            Destroy(img.gameObject);
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
                    Vector2 targetPos = (Vector2)_rootTop.InverseTransformPoint(_handTarget.position) + new Vector2(10f, -26f);
                    Vector2 offset;
                    float scale;
                    if (time < 0.35f)
                    {
                        offset = Vector2.Lerp(away, Vector2.zero, PATween.OutCubic(time / 0.35f));
                        scale = 1f;
                    }
                    else if (time < 0.6f)
                    {
                        offset = Vector2.zero;
                        scale = 1f - Mathf.Sin((time - 0.35f) / 0.25f * Mathf.PI) * 0.15f;
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
            _hand.gameObject.SetActive(false);
        }

        // ================================================================ end popup
        private IEnumerator EndPopup(bool win)
        {
            if (win) Play(sfxWin);
            StartCoroutine(FadeMusic(musicVolume * 0.4f, 0.8f));
            StoreBridge.GameEnded();

            RectTransform end = NewRect("EndPopup", _canvasRect);
            Stretch(end);
            // Popup win/lose đã có nút CTA riêng: ẩn thanh CTA ingame.
            _bottomBar.gameObject.SetActive(false);
            _ctaButton = null;
            CanvasGroup group = end.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image dim = end.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            dim.raycastTarget = true;
            Button anywhere = end.gameObject.AddComponent<Button>();
            anywhere.transition = Selectable.Transition.None;
            anywhere.onClick.AddListener(OpenStore);

            RectTransform content = NewRect("Content", end);
            content.sizeDelta = new Vector2(RefWidth, RefHeight);

            // Thân khung (phần kem viền cam) của cả 2 popup đều là 800x1170 px. Popup NICE có thêm chữ NICE nhô ra
            // trái/trên nên ảnh to hơn (881x1242, thân ở x 81..880, y 72..1241): căn theo thân để khỏi lệch.
            const float bodyW = 760f;
            const float px = bodyW / 800f;
            float bodyH = 1170f * px;
            float bottom = -bodyH * 0.5f;
            RectTransform body = NewRect("PopupBody", content);
            body.sizeDelta = new Vector2(bodyW, bodyH);
            body.anchoredPosition = new Vector2(0f, 70f);
            Image button;
            if (win)
            {
                Vector2 imageSize = new Vector2(881f * px, 1242f * px);
                Vector2 imageOffset = new Vector2((440.5f - 480.5f) * px, (656.5f - 621f) * px);
                NewImage("NicePanel", body, winPanelSprite, imageSize, imageOffset);
                // Nửa trên thân là chú chó (tới ~57% chiều cao); nội dung đặt bên dưới.
                for (int k = 0; k < _target; k++)
                {
                    float x = (k - (_target - 1) * 0.5f) * 110f;
                    NewImage("Star", body, starOnSprite, new Vector2(96f, 96f), new Vector2(x, bottom + 385f));
                }
                NewText("Question", body, "Do you want to play\na harder level?", 48f, Brown, new Vector2(660f, 130f), new Vector2(0f, bottom + 268f));
                button = NewImage("HardButton", body, hardButtonSprite, new Vector2(390f, 390f * 237f / 537f), new Vector2(0f, bottom + 112f));
                NewOutlinedText(button.transform, "PLAY HARD", 60f, Color.white, new Color32(140, 18, 8, 255), new Vector2(420f, 150f), new Vector2(0f, 8f), 4f);
            }
            else
            {
                NewImage("LosePanel", body, losePanelSprite, new Vector2(bodyW, bodyH), Vector2.zero);
                float iconW = 560f;
                float iconH = iconW * 532f / 504f;
                NewImage("LoseIcon", body, loseIconSprite, new Vector2(iconW, iconH), new Vector2(0f, bodyH * 0.5f - 50f - iconH * 0.5f));
                string reason = _timeUp ? "Time's up!" : "The tray is full!";
                NewText("Reason", body, reason, 58f, new Color32(214, 40, 30, 255), new Vector2(660f, 80f), new Vector2(0f, bottom + 405f));
                NewText("Question", body, "Do you want to try again?", 48f, Brown, new Vector2(680f, 80f), new Vector2(0f, bottom + 320f));
                button = NewImage("TryAgainButton", body, buttonSprite, new Vector2(420f, 420f * 237f / 537f), new Vector2(0f, bottom + 160f));
                NewOutlinedText(button.transform, "TRY AGAIN", 66f, Color.white, new Color32(206, 84, 8, 255), new Vector2(420f, 150f), new Vector2(0f, 8f), 4f);
            }
            button.raycastTarget = true;
            Button btn = button.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OpenStore);

            body.localScale = Vector3.zero;
            button.rectTransform.localScale = Vector3.zero;
            yield return StartCoroutine(PATween.Fade(group, 0f, 1f, 0.3f));
            yield return StartCoroutine(PATween.Scale(body, Vector3.zero, Vector3.one, 0.45f, PATween.OutBack));
            yield return StartCoroutine(PATween.Scale(button.rectTransform, Vector3.zero, Vector3.one, 0.35f, PATween.OutBack));

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

        private static float HX(float px) { return (px - 540f) * HeaderScale; }
        // Toạ độ trên ảnh header (px, gốc trên-trái) -> toạ độ trong khung header (pivot ở mép trên).
        private static float HY(float py) { return -py * HeaderScale; }

        private void BuildHeader()
        {
            _header = NewRect("Header", _root);
            _header.pivot = new Vector2(0.5f, 1f);
            // Cao 0 + pivot trên: tâm khung trùng mép trên, nên toạ độ các phần con (anchor giữa) tính từ mép trên.
            _header.sizeDelta = new Vector2(HeaderW, 0f);
            _header.anchoredPosition = new Vector2(0f, HeaderTop);

            // 3 mảnh: trên | dải giữa kéo dài thêm _headerExtra | dưới. Không dùng Sliced để giữ tỉ lệ đều.
            float topH = HeaderSplitPx * HeaderScale;
            float bottomH = HeaderH - topH;
            NewImage("Top", _header, headerTopSprite, new Vector2(HeaderW, topH), new Vector2(0f, -topH * 0.5f));
            if (_headerExtra > 0f)
            {
                NewImage("Mid", _header, headerMidSprite, new Vector2(HeaderW, _headerExtra + 2f), new Vector2(0f, -topH - _headerExtra * 0.5f));
            }
            NewImage("Bottom", _header, headerBottomSprite, new Vector2(HeaderW, bottomH), new Vector2(0f, -topH - _headerExtra - bottomH * 0.5f));

            // Tab trên (ảnh 1080x414): chữ LEVEL có sẵn ở (370,118), số level ngay dưới; ô trắng timer x 450..765.
            NewOutlinedText(_header, levelNumber.ToString(), 64f, Color.white, Brown, new Vector2(200f, 80f), new Vector2(HX(370f), HY(172f)), 4f);
            _timerText = NewText("Timer", _header, "", 64f, Brown, new Vector2(260f, 90f), new Vector2(HX(660f), HY(150f)));
            _timeLeft = timeLimit;
            UpdateTimerText();

            // Dải tím (y 225..285): chủ đề. Dải kem (y 290..378): dòng các từ.
            string title = string.IsNullOrEmpty(categoryTitle) ? "WORDS" : categoryTitle.ToUpper();
            NewText("Title", _header, title, 46f, Color.white, new Vector2(700f, 60f), new Vector2(-40f, HY(255f) + 2f));
            BuildWordLine(HY(HeaderSplitPx) - _headerExtra * 0.5f);
        }

        private static float WordWidth(string word)
        {
            float width = 0f;
            for (int i = 0; i < word.Length; i++)
            {
                width += Advance(word[i]);
                if (i > 0) width += LineTracking;
            }
            return width;
        }

        private static float WordSpace()
        {
            return LineFont * 0.42f;
        }

        private float LineWidth(List<int> line)
        {
            float width = 0f;
            for (int k = 0; k < line.Count; k++)
            {
                if (k > 0) width += WordSpace();
                width += WordWidth(_words[line[k]]);
            }
            return width;
        }

        /// <summary>
        /// Chia các từ thành dòng: vừa 1 dòng (cho phép thu nhỏ tới MinLineScale) thì giữ 1 dòng,
        /// không thì xuống dòng theo từ, mỗi dòng căn giữa. Header kéo dài thêm cho các dòng mới.
        /// </summary>
        private void PrepareLineLayout()
        {
            _lines = new List<List<int>>();
            List<int> all = new List<int>();
            for (int w = 0; w < _words.Length; w++) all.Add(w);
            float total = LineWidth(all);
            if (LineMaxWidth / total >= MinLineScale)
            {
                _lines.Add(all);
                _lineScale = Mathf.Min(1f, LineMaxWidth / total);
            }
            else
            {
                List<int> current = new List<int>();
                float width = 0f;
                for (int w = 0; w < _words.Length; w++)
                {
                    float ww = WordWidth(_words[w]);
                    if (current.Count > 0 && width + WordSpace() + ww > LineMaxWidth)
                    {
                        _lines.Add(current);
                        current = new List<int>();
                        width = 0f;
                    }
                    if (current.Count > 0) width += WordSpace();
                    current.Add(w);
                    width += ww;
                }
                if (current.Count > 0) _lines.Add(current);
                _lineScale = 1f;
                for (int l = 0; l < _lines.Count; l++) _lineScale = Mathf.Min(_lineScale, LineMaxWidth / LineWidth(_lines[l]));
            }
            _headerExtra = (_lines.Count - 1) * LineSpacing * _lineScale;
        }

        /// <summary>Vị trí khay, board, ô thông báo phụ thuộc chiều cao header; board thu nhỏ nếu thiếu chỗ.</summary>
        private void ComputeLayout()
        {
            float trayWidth = _traySize * TraySlot + (_traySize - 1) * TrayGap;
            _trayScale = Mathf.Min(1f, MaxRowWidth / trayWidth);
            float trayHalf = TraySlot * 0.5f * _trayScale;

            float headerBottom = HeaderTop - HeaderH - _headerExtra;
            _trayY = headerBottom - 30f - trayHalf;
            _boardTop = _trayY - trayHalf - 40f;
            float boardH = _rows * CellH;
            float fitHeight = (_boardTop - MinBoardBottom) / boardH;
            float fitWidth = MaxRowWidth / (_cols * CellW);
            _boardScale = Mathf.Clamp(Mathf.Min(fitHeight, fitWidth), 0.4f, 1f);
            _messageY = _boardTop - boardH * _boardScale - 62f;
        }

        private void BuildWordLine(float y)
        {
            _wordLine = NewRect("WordLine", _header);
            _wordLine.anchoredPosition = new Vector2(0f, y);
            _wordLine.localScale = new Vector3(_lineScale, _lineScale, 1f);

            _lineLabels = new TextMeshProUGUI[_words.Length][];
            for (int l = 0; l < _lines.Count; l++)
            {
                List<int> line = _lines[l];
                float lineY = ((_lines.Count - 1) * 0.5f - l) * LineSpacing;
                float x = -LineWidth(line) * 0.5f;
                for (int k = 0; k < line.Count; k++)
                {
                    int w = line[k];
                    if (k > 0) x += WordSpace();
                    _lineLabels[w] = new TextMeshProUGUI[_words[w].Length];
                    for (int i = 0; i < _words[w].Length; i++)
                    {
                        if (i > 0) x += LineTracking;
                        float adv = Advance(_words[w][i]);
                        TextMeshProUGUI label = NewText("L_" + _words[w][i], _wordLine, _words[w][i].ToString(), LineFont, PendingColor, new Vector2(adv + 30f, LineFont * 1.3f), new Vector2(x + adv * 0.5f, lineY));
                        _lineLabels[w][i] = label;
                        x += adv;
                    }
                }
            }
        }

        private static float Advance(char ch)
        {
            if (ch < 'A' || ch > 'Z') return LineFont * 0.6f;
            return LetterAdvance[ch - 'A'] * LineFont / 100f;
        }

        private static void SetLabelColor(TextMeshProUGUI label, Color color)
        {
            label.color = color;
        }

        private void UpdateTimerText()
        {
            int seconds = Mathf.CeilToInt(_timeLeft);
            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            int m = seconds / 60;
            int s = seconds % 60;
            _timerText.text = (m < 10 ? "0" : "") + m + ":" + (s < 10 ? "0" : "") + s;
            // Luna build dùng compiler cũ: ép kiểu rõ ràng, không trộn Color32 và Color trong toán tử ?:.
            _timerText.color = seconds <= 10 ? TimerWarnColor : Brown;
        }

        private void BuildTray()
        {
            _trayLayer = NewRect("Tray", _root);
            _trayLayer.anchoredPosition = new Vector2(0f, _trayY);
            _traySlots = new Image[_traySize];
            _trayWarnings = new Image[_traySize];
            for (int i = 0; i < _traySize; i++)
            {
                float x = (i - (_traySize - 1) * 0.5f) * (TraySlot + TrayGap);
                Image warn = NewImage("Warn" + i, _trayLayer, trayWarningSprite, new Vector2(TraySlot * 1.25f, TraySlot * 1.25f), new Vector2(x, 0f));
                warn.enabled = false;
                _trayWarnings[i] = warn;
                Image slot = NewImage("Slot" + i, _trayLayer, slotSprite, new Vector2(TraySlot, TraySlot), new Vector2(x, 0f));
                slot.color = new Color32(255, 243, 214, 255);
                _traySlots[i] = slot;
            }
        }

        private void BuildStarBar()
        {
            float width = _target * StarSpacing + 70f;
            _starBar = NewRounded("StarBar", _root, new Vector2(width, 108f), new Vector2(0f, StarBarY), new Color(0.22f, 0.04f, 0.33f, 0.62f));

            _stars = new Image[_target];
            for (int k = 0; k < _target; k++)
            {
                float x = (k - (_target - 1) * 0.5f) * StarSpacing;
                if (k > 0)
                {
                    Image link = NewImage("Link", _starBar, null, new Vector2(StarSpacing - 70f, 10f), new Vector2(x - StarSpacing * 0.5f, 0f));
                    link.color = new Color(1f, 1f, 1f, 0.35f);
                }
                _stars[k] = NewImage("Star" + k, _starBar, starOffSprite, new Vector2(92f, 92f), new Vector2(x, 2f));
            }
        }

        private void BuildTopLayers()
        {
            _rootTop = NewRect("RootTop", _canvasRect);
            _rootTop.sizeDelta = new Vector2(RefWidth, RefHeight);
            _fxLayer = NewRect("FxLayer", _rootTop);

            Image hand = NewImage("Hand", _rootTop, handSprite, new Vector2(150f, 150f * 173f / 181f), Vector2.zero);
            _hand = hand.rectTransform;
            _hand.pivot = new Vector2(0.1f, 0.92f);
            _hand.gameObject.SetActive(false);

            // Ô thông báo vẽ SAU bàn tay: khi tay chỉ ô hàng dưới cùng, thân tay nằm dưới ô thông báo, chữ không bị che.
            RectTransform pill = NewRounded("Message", _rootTop, new Vector2(760f, 86f), new Vector2(0f, _messageY), new Color(0.25f, 0.06f, 0.36f, 0.92f));
            _messageGroup = pill.gameObject.AddComponent<CanvasGroup>();
            _messageGroup.blocksRaycasts = false;
            _message = NewText("Text", pill.transform, "", 44f, Color.white, new Vector2(720f, 80f), new Vector2(0f, 2f));

            RectTransform flashRect = NewRect("RedFlash", _canvasRect);
            Stretch(flashRect);
            _flash = flashRect.gameObject.AddComponent<Image>();
            _flash.color = new Color(1f, 0.05f, 0.05f, 0f);
            _flash.raycastTarget = false;

            // Viền đỏ quanh màn hình (TrayWarningEdges của game gốc, màu trắng -> tô đỏ).
            RectTransform edgesRect = NewRect("RedEdges", _canvasRect);
            Stretch(edgesRect);
            _flashEdges = edgesRect.gameObject.AddComponent<Image>();
            _flashEdges.sprite = flashEdgesSprite;
            _flashEdges.color = new Color(1f, 0.08f, 0.05f, 0f);
            _flashEdges.raycastTarget = false;
        }

        /// <summary>Thanh dưới cùng: logo + tên app + nút PLAY NOW. Luôn hiển thị, bấm vào mở store.</summary>
        private void BuildBottomBar()
        {
            _bottomBar = NewRect("BottomBar", _canvasRect);
            _bottomBar.anchorMin = new Vector2(0f, 0f);
            _bottomBar.anchorMax = new Vector2(1f, 0f);
            _bottomBar.pivot = new Vector2(0.5f, 0f);
            _bottomBar.sizeDelta = new Vector2(0f, BottomBarHeight);
            _bottomBar.anchoredPosition = Vector2.zero;
            Image bg = _bottomBar.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 0.98f, 0.93f, 0.97f);
            bg.raycastTarget = true;
            Button barButton = _bottomBar.gameObject.AddComponent<Button>();
            barButton.transition = Selectable.Transition.None;
            barButton.onClick.AddListener(OpenStore);

            RectTransform line = NewRect("TopLine", _bottomBar);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.sizeDelta = new Vector2(0f, 8f);
            line.anchoredPosition = Vector2.zero;
            Image lineImg = line.gameObject.AddComponent<Image>();
            lineImg.color = new Color32(150, 20, 200, 255);
            lineImg.raycastTarget = false;

            const float logoH = 160f;
            float logoW = logoH * 1050f / 719f;
            Image logo = NewImage("Logo", _bottomBar, logoSprite, new Vector2(logoW, logoH), new Vector2(24f + logoW * 0.5f, -4f));
            AnchorLeft(logo.rectTransform);

            TextMeshProUGUI name = NewText("AppName", _bottomBar, "Tile Word\nExplorer", 44f, Brown, new Vector2(300f, 150f), new Vector2(24f + logoW + 14f + 150f, -4f));
            name.alignment = TextAlignmentOptions.Left;
            name.lineSpacing = -12f;
            AnchorLeft(name.rectTransform);

            Vector2 buttonSize = new Vector2(330f, 330f * 237f / 537f);
            Image button = NewImage("PlayNow", _bottomBar, buttonSprite, buttonSize, new Vector2(-24f - buttonSize.x * 0.5f, -2f));
            button.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            button.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            button.raycastTarget = true;
            Button play = button.gameObject.AddComponent<Button>();
            play.transition = Selectable.Transition.None;
            play.onClick.AddListener(OpenStore);
            NewOutlinedText(button.transform, "PLAY NOW", 54f, Color.white, new Color32(206, 84, 8, 255), new Vector2(320f, 120f), new Vector2(0f, 6f), 3.5f);
            _ctaButton = button.rectTransform;
        }

        private static void AnchorLeft(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
        }

        private void ShowMessage(string text)
        {
            _message.text = text;
            StartCoroutine(MessageRoutine());
        }

        private void HideMessage()
        {
            StartCoroutine(PATween.Fade(_messageGroup, _messageGroup.alpha, 0f, 0.2f));
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

        // ================================================================ helpers
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

        /// <summary>
        /// Nền bo góc ghép từ 3 ảnh thường: mép trái | khối giữa | mép phải (mép trái lật ngang).
        /// Không dùng Image.Type.Sliced: bản build Luna web vẽ Sliced thành hình chữ nhật góc vuông.
        /// </summary>
        private RectTransform NewRounded(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            float h = size.y;
            float capW = Mathf.Min(h * 48f / 128f, size.x * 0.5f);
            float half = size.x * 0.5f;
            Image left = NewImage("CapL", rt, pillCapSprite, new Vector2(capW, h), new Vector2(-half + capW * 0.5f, 0f));
            left.color = color;
            Image right = NewImage("CapR", rt, pillCapSprite, new Vector2(capW, h), new Vector2(half - capW * 0.5f, 0f));
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            right.color = color;
            float midW = size.x - capW * 2f;
            if (midW > 0f)
            {
                Image mid = NewImage("Mid", rt, null, new Vector2(midW, h), Vector2.zero);
                mid.color = color;
            }
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

        /// <summary>Chữ có viền giả: 8 bản sao lệch quanh chữ chính + bóng (không cần material outline của TMP).</summary>
        private TextMeshProUGUI NewOutlinedText(Transform parent, string text, float fontSize, Color fill, Color outline, Vector2 size, Vector2 pos, float o)
        {
            Vector2[] offsets =
            {
                new Vector2(-o, 0f), new Vector2(o, 0f), new Vector2(0f, -o), new Vector2(0f, o),
                new Vector2(-o, -o), new Vector2(o, -o), new Vector2(-o, o), new Vector2(o, o),
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                NewText("Outline", parent, text, fontSize, outline, size, pos + offsets[i]);
            }
            NewText("Shadow", parent, text, fontSize, outline, size, pos + new Vector2(0f, -o * 2f));
            return NewText("Label", parent, text, fontSize, fill, size, pos);
        }
    }
}
