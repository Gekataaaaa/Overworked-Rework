using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Overworked.EditorTools
{
    /// <summary>
    /// Лампы для сборщиков арта этажей (Floor02Art, Floor01Art) — как на Floor_03 (Floor03Lamps):
    /// нарисованные трубка Light_Tube_On + мягкий конус Light_Cone_Soft, под ними Lamp_N с Light2D и LampLight
    /// (картинки повторяют яркость света), по желанию LightFlicker (мерцание на низком рассудке)
    /// и BackgroundDimmer (фон темнеет, когда лампы гаснут).
    /// </summary>
    internal static class FloorLamps
    {
        const string SoftConePath = "Assets/_Project/Art/Effects/Light_Cone_Soft.png";

        /// <summary>Одна лампа: позиции трубки и конуса в локальных координатах группы Lights.</summary>
        internal struct Fixture
        {
            public Vector3 Tube, Cone;
            /// <summary>Эта лампа мигает всегда, независимо от Style.Flicker (неисправная лампа).</summary>
            public bool Faulty;
            public Fixture(Vector3 tube, Vector3 cone, bool faulty = false) { Tube = tube; Cone = cone; Faulty = faulty; }
        }

        /// <summary>Настройки света ламп одной группы.</summary>
        internal struct Style
        {
            public Color Color;
            public float Intensity;
            /// <summary>Все лампы мерцают на низком рассудке (LightFlicker с настройками по умолчанию).</summary>
            public bool Flicker;
            public float DarkLevel;
            /// <summary>Насколько приглушить нарисованный конус: 0 — как есть, 0.5 — вдвое прозрачнее.</summary>
            public float ConeDim;
        }

        /// <summary>Холодный свет, как на Floor_03.</summary>
        internal static readonly Style Office = new Style { Color = new Color(0.85f, 0.95f, 0.85f), Intensity = 0.6f, DarkLevel = 0.5f };

        /// <summary>
        /// Пересобирает лампы в группе lights: всё старое внутри удаляется. background — группа фона,
        /// которую затемняет BackgroundDimmer (null — без затемнения).
        /// </summary>
        internal static void Build(Transform lights, Sprite tube, Sprite cone, IList<Fixture> fixtures, Style style,
            Transform background, int order = -6)
        {
            for (int i = lights.childCount - 1; i >= 0; i--) Object.DestroyImmediate(lights.GetChild(i).gameObject);
            if (fixtures == null || fixtures.Count == 0) return;

            // Light_Cone.png с яркой полосой у нижнего края; Soft — та же картинка без неё (см. Floor03Lamps).
            var soft = AssetDatabase.LoadAssetAtPath<Sprite>(SoftConePath);
            var links = new List<LampLight>();
            for (int i = 0; i < fixtures.Count; i++)
            {
                var f = fixtures[i];
                var tubeSr = MakeSprite(lights, $"Light_Tube_On_{i + 1}", tube, f.Tube, order);
                var coneSr = MakeSprite(lights, $"Light_Cone_{i + 1}", soft != null ? soft : cone, f.Cone, order);
                if (soft != null && cone != null)
                {
                    // Мягкий конус может быть другого размера в пикселях — подгоняем под исходный.
                    var s = new Vector3(cone.bounds.size.x / soft.bounds.size.x, cone.bounds.size.y / soft.bounds.size.y, 1f);
                    coneSr.transform.localScale = s;
                }
                // LampLight запоминает этот цвет как «обычный» и масштабирует от него — приглушение сохраняется.
                if (style.ConeDim > 0f) coneSr.color = new Color(1f, 1f, 1f, 1f - style.ConeDim);

                var go = new GameObject($"Lamp_{i + 1}");
                go.transform.SetParent(lights, false);
                // Свет — посередине конуса, по X — под трубкой.
                go.transform.localPosition = new Vector3(f.Tube.x, f.Cone.y, 0f);
                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = style.Color;
                light.intensity = style.Intensity;
                light.pointLightInnerRadius = 0.5f;
                light.pointLightOuterRadius = Mathf.Max(2.5f, coneSr.bounds.size.y * 0.8f);

                var link = go.AddComponent<LampLight>();
                Set(link, ("tube", tubeSr), ("cone", coneSr));
                if (f.Faulty)
                {
                    // Неисправная лампа: мигает с любого рассудка (порог 1) и часто, сильнее — при падении рассудка.
                    var flicker = new SerializedObject(go.AddComponent<LightFlicker>());
                    flicker.FindProperty("flickerBelowSanity").floatValue = 1f;
                    flicker.FindProperty("dropoutRate").floatValue = 12f;
                    flicker.FindProperty("dropoutLevel").floatValue = 0.05f;
                    flicker.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (style.Flicker) go.AddComponent<LightFlicker>();
                links.Add(link);
            }

            if (background == null) return;
            var dimmerGo = new GameObject("BackgroundDimmer");
            dimmerGo.transform.SetParent(lights, false);
            var dimmer = new SerializedObject(dimmerGo.AddComponent<BackgroundDimmer>());
            var lampList = dimmer.FindProperty("lamps");
            lampList.arraySize = links.Count;
            for (int i = 0; i < links.Count; i++) lampList.GetArrayElementAtIndex(i).objectReferenceValue = links[i];
            var bg = background.GetComponentsInChildren<SpriteRenderer>(true);
            var bgList = dimmer.FindProperty("background");
            bgList.arraySize = bg.Length;
            for (int i = 0; i < bg.Length; i++) bgList.GetArrayElementAtIndex(i).objectReferenceValue = bg[i];
            dimmer.FindProperty("darkLevel").floatValue = style.DarkLevel;
            // Как на Floor_03: гаснут лампы — темнеет всё на этаже (фон, двери, предметы, Дэнни), кроме самих ламп.
            dimmer.FindProperty("wholeScene").boolValue = true;
            dimmer.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Ровная подсветка всей комнаты 1920×1080 (19.2 × 10.8 юнита): большой мягкий точечный свет в центре.
        /// Для слишком тёмных фонов — поднимает их без правки картинки. Вызывать после Build (Build очищает группу).
        /// </summary>
        internal static void AddAmbient(Transform lights, float intensity, Color color)
        {
            var go = new GameObject("Ambient");
            go.transform.SetParent(lights, false);
            go.transform.localPosition = Vector3.zero;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            // Внутренний радиус накрывает почти всю комнату — свет ровный, края гаснут за стенами.
            light.pointLightInnerRadius = 9f;
            light.pointLightOuterRadius = 14f;
        }

        static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Vector3 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void Set(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
