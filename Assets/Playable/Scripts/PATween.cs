using System.Collections;
using UnityEngine;

namespace PA.TileWord
{
    /// <summary>
    /// Tween nhỏ dựa trên coroutine, thay cho DOTween (Luna không cần package ngoài).
    /// Lưu ý Luna: luôn chạy các IEnumerator này bằng StartCoroutine (kể cả khi chờ trong coroutine khác:
    /// yield return StartCoroutine(...)), không yield IEnumerator trực tiếp.
    /// </summary>
    public static class PATween
    {
        public static float Linear(float t) { return t; }
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InCubic(float t) { return t * t * t; }
        public static float InOutSine(float t) { return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f; }

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        /// <summary>Rơi xuống rồi nảy nhẹ khi chạm đích (nảy thấp hơn bounce chuẩn cho mảnh gỗ "nặng").</summary>
        public static float OutBounce(float t)
        {
            if (t < 0.7f)
            {
                float a = t / 0.7f;
                return a * a;
            }
            float b = (t - 0.7f) / 0.3f;
            return 1f - Mathf.Sin(b * Mathf.PI) * 0.035f;
        }

        public delegate float Ease(float t);
        public delegate void Step(float easedT);

        /// <summary>Chạy step(eased t) từ 0 đến 1 trong duration giây.</summary>
        public static IEnumerator Run(float duration, Ease ease, Step step)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                step(ease(t));
                yield return null;
            }
        }

        public static IEnumerator Scale(Transform target, Vector3 from, Vector3 to, float duration, Ease ease)
        {
            return Run(duration, ease, delegate (float t)
            {
                if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, t);
            });
        }

        /// <summary>Phồng lên rồi trở lại scale gốc.</summary>
        public static IEnumerator Punch(Transform target, float amount, float duration)
        {
            // Vòng lặp phẳng: không yield IEnumerator lồng nhau (Luna xử lý sai khi lồng >= 3 tầng).
            Vector3 baseScale = target.localScale;
            float time = 0f;
            while (time < duration && target != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI) * amount;
                target.localScale = baseScale * (1f + k);
                yield return null;
            }
            if (target != null) target.localScale = baseScale;
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            return Run(duration, Linear, delegate (float t)
            {
                if (group != null) group.alpha = Mathf.Lerp(from, to, t);
            });
        }
    }
}
