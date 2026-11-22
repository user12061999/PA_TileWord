using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PA.TileWord
{
    /// <summary>Một ô chữ trên bảng 10×10.</summary>
    public class LetterTile : MonoBehaviour, IPointerDownHandler
    {
        public static readonly Color NormalColor = Color.white;
        public static readonly Color SelectedColor = new Color(1f, 0.84f, 0.42f, 1f);
        public static readonly Color WrongColor = new Color(1f, 0.62f, 0.55f, 1f);
        public static readonly Color LetterColor = new Color32(107, 58, 30, 255);

        public char Letter;
        public int Row;
        public int Col;
        public RectTransform Rect;
        public Image Background;
        public TextMeshProUGUI Label;

        public bool Selected { get; private set; }
        public bool Used;

        public System.Action<LetterTile> Tapped;

        private Coroutine _shake;
        private Vector2 _shakeBase;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Tapped != null) Tapped(this);
        }

        public void SetSelected(bool selected)
        {
            Selected = selected;
            Background.color = selected ? SelectedColor : NormalColor;
            Rect.localScale = selected ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
        }

        /// <summary>Rung nhẹ khi chọn sai chữ (không tính là thua).</summary>
        public void Shake()
        {
            if (_shake != null)
            {
                StopCoroutine(_shake);
                Rect.anchoredPosition = _shakeBase;
            }
            _shakeBase = Rect.anchoredPosition;
            _shake = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            const float duration = 0.32f;
            float time = 0f;
            Background.color = WrongColor;
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(time / duration);
                Rect.anchoredPosition = _shakeBase + new Vector2(Mathf.Sin(time * 70f) * 12f * k, 0f);
                yield return null;
            }
            Rect.anchoredPosition = _shakeBase;
            Background.color = Selected ? SelectedColor : NormalColor;
            _shake = null;
        }
    }
}
