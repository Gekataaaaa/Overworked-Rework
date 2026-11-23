using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Печать текста по буквам для окон интерфейса (записка, подсказка управления, тосты, телефон)
    /// со звуком «бегущего текста» (UiSounds). Работает через maxVisibleCharacters, поэтому теги
    /// &lt;color&gt; и т. п. не мешают. Время — реальное: окна печатаются и на паузе эффектов.
    /// </summary>
    public static class Typewriter
    {
        /// <summary>
        /// Печатает уже выставленный текст t. skip() == true — сразу показать всё (игрок нажал «продолжить»).
        /// </summary>
        public static IEnumerator Type(TMP_Text t, float charsPerSecond, Func<bool> skip = null, bool sound = true)
        {
            if (t == null) yield break;
            t.maxVisibleCharacters = 0;
            t.ForceMeshUpdate();
            int total = t.textInfo.characterCount;
            if (charsPerSecond > 0f)
            {
                float shown = 0f;
                while (shown < total)
                {
                    if (skip != null && skip()) break;
                    shown += charsPerSecond * Time.unscaledDeltaTime;
                    int visible = Mathf.Min(total, Mathf.FloorToInt(shown));
                    if (sound && visible > t.maxVisibleCharacters) UiSounds.TypingTick();
                    t.maxVisibleCharacters = visible;
                    yield return null;
                }
            }
            t.maxVisibleCharacters = int.MaxValue;
            if (sound) UiSounds.TypingStop();
        }

        /// <summary>Мгновенно показывает весь текст (печать прервана).</summary>
        public static void ShowAll(TMP_Text t)
        {
            if (t != null) t.maxVisibleCharacters = int.MaxValue;
        }
    }
}
