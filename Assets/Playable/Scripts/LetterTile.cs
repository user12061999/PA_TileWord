using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PA.TileWord
{
    /// <summary>Một ô chữ trên board (hoặc trong khay sau khi chọn sai).</summary>
    public class LetterTile : MonoBehaviour, IPointerDownHandler
    {
        public static readonly Color NormalColor = Color.white;
        public static readonly Color WrongColor = new Color(1f, 0.55f, 0.5f, 1f);
        public static readonly Color LetterColor = new Color32(107, 58, 30, 255);

        public char Letter;
        public int Row;
        public int Col;
        public RectTransform Rect;
        public Image Background;
        public TextMeshProUGUI Label;

        /// <summary>Đã rời board (đang bay về từ / khay). Không nhận tap nữa.</summary>
        public bool Used;

        public System.Action<LetterTile> Tapped;
        public System.Action<LetterTile> Landed;

        private Coroutine _move;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Tapped != null) Tapped(this);
        }

        /// <summary>Rơi tới vị trí <paramref name="target"/> (anchoredPosition) sau <paramref name="delay"/> giây; hủy lần rơi trước.</summary>
        public void FallTo(Vector2 from, Vector2 target, float delay, float duration)
        {
            StopMove();
            Rect.anchoredPosition = from;
            _move = StartCoroutine(FallRoutine(from, target, delay, duration));
        }

        /// <summary>Ô mới xuất hiện tại chỗ: phóng to từ 0 (không bay ngang qua khay/header).</summary>
        public void PopIn(float delay)
        {
            Rect.localScale = Vector3.zero;
            StartCoroutine(PopRoutine(delay));
        }

        private IEnumerator PopRoutine(float delay)
        {
            float time = -delay;
            const float duration = 0.25f;
            while (time < duration)
            {
                time += Time.deltaTime;
                if (Used) yield break;
                float s = time <= 0f ? 0f : PATween.OutBack(Mathf.Clamp01(time / duration));
                Rect.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (!Used) Rect.localScale = Vector3.one;
        }

        public void StopMove()
        {
            if (_move != null)
            {
                StopCoroutine(_move);
                _move = null;
            }
        }

        private IEnumerator FallRoutine(Vector2 from, Vector2 target, float delay, float duration)
        {
            float time = -delay;
            bool landed = false;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                Rect.anchoredPosition = Vector2.LerpUnclamped(from, target, PATween.OutBounce(t));
                // OutBounce chạm đích ở t = 0.7.
                if (!landed && t >= 0.7f)
                {
                    landed = true;
                    if (Landed != null) Landed(this);
                }
                yield return null;
            }
            Rect.anchoredPosition = target;
            _move = null;
        }
    }
}
