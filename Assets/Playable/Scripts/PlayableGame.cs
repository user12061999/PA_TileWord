using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PA.TileWord
{
    /// <summary>
    /// Playable Tile Word Explorer: Intro -> Tutorial (CAT) -> Free play (DOG, FISH) -> End card.
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
        public AudioClip sfxTap;
        public AudioClip sfxWrong;
        public AudioClip sfxWord;
        public AudioClip sfxWin;

        [Header("Tuning")]
        public float introDuration = 1.5f;
        public float idleHintDelay = 3f;

        [Header("Luna Playground")]
        [LunaPlaygroundField("Refill board after a word", 0, "Gameplay")]
        [Tooltip("Sau khi clear 1 từ: các ô phía trên rơi xuống lấp chỗ trống và chữ mới rơi từ trên vào.")]
        public bool refillBoard = true;

        [LunaPlaygroundField("Words completed to show CTA", 1, "Gameplay")]
        [LunaPlaygroundFieldStep(1)]
        [Range(1, 3)]
        [Tooltip("Số từ (tính cả CAT ở tutorial) người chơi cần hoàn thành thì hiện end card. 1 = ngay sau tutorial, 3 = tất cả.")]
        public int wordsToShowCta = 3;

        // ---------------------------------------------------------------- data
        private const float RefWidth = 1080f;
        private const float RefHeight = 1920f;
        private const int GridSize = 10;
        private const float CellW = 98f;
        private const float CellH = 100f;
        private const float TileW = 92f;
        private const float TileH = 100f;
        private const float SlotSize = 66f;

        // Chữ "nhiễu": không chứa chữ nào của C,A,T,D,O,G,F,I,S,H để người chơi không bị nhầm.
        private const string FillerLetters = "BEJKLMNPQRUVWYZ";

        private static readonly string[] GridRows =
        {
            "PLUMEKRBNW",
            "ZRENBUYQEL",
            "KUBMEYMVJP",
            "WLEYNPRNUZ",
            "NBQJLKEMRV",
            "MEVRWLUBKY",
            "UPZKEBYWER",
            "JKMNRYUEBL",
            "QVBEUPLNZK",
            "RYNKMWJEVU",
        };

        // Từ 0 là tutorial. Mỗi chữ của từ nằm ở một ô riêng, rải rác (không cần liền nhau):
        // WordCells[w] = { hàng0, cột0, hàng1, cột1, ... } theo thứ tự chữ trong từ.
        private static readonly string[] Words = { "CAT", "DOG", "FISH" };
        private static readonly int[][] WordCells =
        {
            new[] { 2, 1, 4, 5, 1, 7 },
            new[] { 6, 2, 3, 3, 8, 6 },
            new[] { 5, 8, 7, 4, 0, 3, 9, 8 },
        };

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
        private AudioSource _audio;

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
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;

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
            return Mathf.Clamp(wordsToShowCta, 1, Words.Length);
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

            float time = 0f;
            bool done = false;
            while (!done)
            {
                time += Time.deltaTime;
                done = true;
                for (int i = 0; i < count; i++)
                {
                    LetterTile tile = _tiles[i / GridSize, i % GridSize];
                    float t = Mathf.Clamp01((time - delay[i]) / fallTime);
                    if (t < 1f) done = false;
                    tile.Rect.anchoredPosition = Vector2.LerpUnclamped(from[i], to[i], PATween.OutBounce(t));
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
            ShowMessage("Tap C - A - T to find CAT!");
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
                // Chạm lại chữ cuối cùng để bỏ chọn.
                if (index == _selection.Count - 1)
                {
                    _selection.RemoveAt(index);
                    tile.SetSelected(false);
                    Play(sfxTap);
                }
                RefreshTutorialHand();
                return;
            }

            string candidate = SelectionString() + tile.Letter;
            int completed = -1;
            bool isPrefix = false;
            for (int w = 0; w < Words.Length; w++)
            {
                if (_cards[w].Found) continue;
                if (_phase == Phase.Tutorial && w != 0) continue;
                if (!StartsWith(Words[w], candidate)) continue;
                isPrefix = true;
                if (Words[w].Length == candidate.Length) completed = w;
            }

            if (!isPrefix)
            {
                tile.Shake();
                Play(sfxWrong);
                RefreshTutorialHand();
                return;
            }

            _selection.Add(tile);
            tile.SetSelected(true);
            Play(sfxTap);

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

        private LetterTile NextHintTile()
        {
            string current = SelectionString();
            for (int w = 0; w < Words.Length; w++)
            {
                if (_cards[w].Found) continue;
                if (_phase == Phase.Tutorial && w != 0) continue;
                if (!StartsWith(Words[w], current)) continue;
                if (current.Length >= Words[w].Length) continue;
                LetterTile candidate = _wordTiles[w][current.Length];
                if (!candidate.Used && !candidate.Selected) return candidate;
            }
            return null;
        }

        private string SelectionString()
        {
            string s = "";
            for (int i = 0; i < _selection.Count; i++) s += _selection[i].Letter;
            return s;
        }

        private static bool StartsWith(string word, string prefix)
        {
            if (prefix.Length > word.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (word[i] != prefix[i]) return false;
            }
            return true;
        }

        // ================================================================ word complete
        private IEnumerator CompleteWord(int wordIndex, List<LetterTile> letters)
        {
            WordCard card = _cards[wordIndex];
            card.Found = true;
            _flyingLetters += letters.Count;
            bool wasTutorial = _phase == Phase.Tutorial;

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

            // 2) Chữ bay về card.
            for (int i = 0; i < letters.Count; i++)
            {
                StartCoroutine(FlyLetter(letters[i], card, i, i * 0.08f));
            }

            // 3) Fill board: ô phía trên rơi xuống lấp chỗ trống, chữ mới rơi từ trên vào.
            if (refillBoard) StartCoroutine(RefillBoard());

            while (CountFilled(card) < letters.Count) yield return null;

            Play(sfxWord);
            Vector2 cardPos = (Vector2)_fxLayer.InverseTransformPoint(card.Rect.position);
            for (int k = 0; k < 8; k++)
            {
                Vector2 offset = Random.insideUnitCircle * new Vector2(card.Rect.rect.width * 0.6f, 50f);
                SpawnSparkle(cardPos + offset, Random.Range(50f, 90f), Random.Range(0.4f, 0.7f));
            }
            yield return StartCoroutine(PATween.Punch(card.Rect, 0.2f, 0.3f));

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
                    char letter = FillerLetters[Random.Range(0, FillerLetters.Length)];
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

            float time = 0f;
            while (time < maxDelay + fallTime)
            {
                time += Time.deltaTime;
                for (int i = 0; i < moving.Count; i++)
                {
                    if (moving[i] == null) continue;
                    float t = Mathf.Clamp01((time - delays[i]) / fallTime);
                    moving[i].Rect.anchoredPosition = Vector2.LerpUnclamped(fromPos[i], toPos[i], PATween.OutBounce(t));
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
            float endScale = SlotSize / TileH;

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
            Play(sfxTap);
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

            Image button = NewImage("ContinueButton", content, buttonSprite, new Vector2(580f, 170f), new Vector2(0f, -330f));
            button.type = Image.Type.Sliced;
            button.raycastTarget = true;
            Button continueButton = button.gameObject.AddComponent<Button>();
            continueButton.transition = Selectable.Transition.None;
            continueButton.onClick.AddListener(OpenStore);
            TextMeshProUGUI label = NewText("Label", button.transform, "CONTINUE", 66f, Color.white, new Vector2(560f, 150f), new Vector2(0f, 6f));
            label.raycastTarget = false;
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
            if (!_storeOpened) Play(sfxTap);
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

            // Dải cam (y 225..285 trên ảnh 414px) và vùng kem (y 290..378).
            float bandY = height * 0.5f - 255f * k;
            float creamY = height * 0.5f - 334f * k;
            NewText("Title", _header, "FIND THE WORDS", 44f, Color.white, new Vector2(800f, 60f), new Vector2(-40f, bandY + 2f));

            float gap = 4f;
            float cardGap = 50f;
            float total = 0f;
            for (int w = 0; w < Words.Length; w++)
            {
                total += Words[w].Length * SlotSize + (Words[w].Length - 1) * gap;
            }
            total += cardGap * (Words.Length - 1);

            _cards = new WordCard[Words.Length];
            float x = -total * 0.5f;
            for (int w = 0; w < Words.Length; w++)
            {
                string word = Words[w];
                float cardWidth = word.Length * SlotSize + (word.Length - 1) * gap;
                RectTransform cardRect = NewRect("Card_" + word, _header);
                cardRect.sizeDelta = new Vector2(cardWidth, SlotSize);
                cardRect.anchoredPosition = new Vector2(x + cardWidth * 0.5f, creamY);

                WordCard card = new WordCard();
                card.Rect = cardRect;
                card.Slots = new Image[word.Length];
                card.Labels = new TextMeshProUGUI[word.Length];
                for (int i = 0; i < word.Length; i++)
                {
                    float sx = -cardWidth * 0.5f + SlotSize * 0.5f + i * (SlotSize + gap);
                    Image slot = NewImage("Slot" + i, cardRect, slotSprite, new Vector2(SlotSize, SlotSize), new Vector2(sx, 0f));
                    TextMeshProUGUI label = NewText("Letter", slot.transform, word[i].ToString(), 42f, Brown, new Vector2(SlotSize, SlotSize), new Vector2(0f, 1f));
                    label.alpha = 0.3f;
                    card.Slots[i] = slot;
                    card.Labels[i] = label;
                }
                _cards[w] = card;
                x += cardWidth + cardGap;
            }
        }

        private void BuildBoard()
        {
            _board = NewRect("Board", _root);
            _board.sizeDelta = new Vector2(CellW * GridSize, CellH * GridSize);
            _board.anchoredPosition = new Vector2(0f, -10f);

            char[,] letters = new char[GridSize, GridSize];
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++) letters[r, c] = GridRows[r][c];
            }
            for (int w = 0; w < Words.Length; w++)
            {
                if (WordCells[w].Length != Words[w].Length * 2)
                {
                    Debug.LogError("[Playable] WordCells của " + Words[w] + " sai số lượng ô");
                    continue;
                }
                for (int i = 0; i < Words[w].Length; i++)
                {
                    letters[WordCells[w][i * 2], WordCells[w][i * 2 + 1]] = Words[w][i];
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

            _wordTiles = new LetterTile[Words.Length][];
            for (int w = 0; w < Words.Length; w++)
            {
                _wordTiles[w] = new LetterTile[Words[w].Length];
                for (int i = 0; i < Words[w].Length; i++)
                {
                    _wordTiles[w][i] = _tiles[WordCells[w][i * 2], WordCells[w][i * 2 + 1]];
                }
            }
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

            Image pill = NewImage("Message", _rootTop, null, new Vector2(820f, 96f), new Vector2(0f, -650f));
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
        private void Play(AudioClip clip)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip);
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
