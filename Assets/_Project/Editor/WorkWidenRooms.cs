using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overworked.EditorTools
{
    /// <summary>
    /// P3, рабочая сцена Work_P1/Floor_03_Work: кухня и столовая шириной в 3 экрана (3 × 19.2 = 57.6 юнита по границе камеры).
    /// Как «Extend Corridor x3» у P1: левая стена на месте, правая уезжает вправо; вместе с ней — граница камеры
    /// (CameraBounds), серый фон (~GrayBackdrop) и всё, что стоит у правой стены (на кухне — дверь в столовую и точка
    /// выхода из неё). Лампы кухни повторяются по новой длине. Ширина задаётся целью, а не множителем: повторный запуск
    /// ничего не меняет. Работает в открытой сцене (сохранить — File → Save), отмена — Ctrl+Z.
    /// Меню Overworked → P3 → Floor_03_Work → Widen Kitchen And Canteen To 3 Screens.
    /// </summary>
    public static class WorkWidenRooms
    {
        const string ScenePath = "Assets/_Project/Scenes/Work_P1/Floor_03_Work.unity";
        const float ScreenWidth = 19.2f;      // ортографическая камера 5.4 при 16:9
        const float TargetWidth = 3f * ScreenWidth;
        const float RightZone = 3f;           // что ближе к правой стене — «конец комнаты», едет вместе с ней
        // Граница камеры по высоте должна быть больше кадра (10.8): если ровно равна, Confiner2D сжимает её в точку
        // и прибивает камеру к центру комнаты. ±5.65 — как у коридора (FloorSkeletonBuilder: HalfHeight + 0.25).
        const float MinHalfHeight = 5.65f;

        [MenuItem("Overworked/P3/Floor_03_Work/Widen Kitchen And Canteen To 3 Screens")]
        public static void Run()
        {
            if (!EnsureScene()) return;
            Undo.SetCurrentGroupName("Widen rooms");
            int group = Undo.GetCurrentGroup();

            string done = Widen("Kitchen", repeatLights: true) + " " + Widen("Canteen", repeatLights: false);

            Undo.CollapseUndoOperations(group);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (Application.isBatchMode) EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Floor_03_Work: {done}");
        }

        static string Widen(string roomName, bool repeatLights)
        {
            var room = GameObject.Find("Environment/Rooms/" + roomName);
            var bounds = room != null ? room.transform.Find("CameraBounds")?.GetComponent<PolygonCollider2D>() : null;
            var right = room != null ? room.transform.Find("Walls/WallRight") : null;
            if (bounds == null || right == null) return $"{roomName}: нет CameraBounds или Walls/WallRight.";

            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var p in bounds.points)
            {
                float x = bounds.transform.TransformPoint(p).x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }
            bool taller = EnsureHeight(bounds);
            float delta = TargetWidth - (maxX - minX);
            // Камеры комнаты помнят форму границы: при выключенной перезагрузке домена (Enter Play Mode Options)
            // кэш Confiner2D живёт и в Play — без сброса камера держит старую узкую границу и не едет за Дэнни.
            int cams = InvalidateConfiners(bounds);
            if (delta < 0.01f)
                return $"{roomName}: уже {maxX - minX:0.#}" + (taller ? ", граница камеры выше кадра" : "") + $", кэш камер сброшен ({cams}).";
            float oldRightWall = right.position.x;

            // 1. Что стоит у правой стены (кроме стен, фона и границы) — едет вместе с ней.
            var toMove = new List<Transform>();
            foreach (Transform child in room.transform)
                Collect(child, oldRightWall, toMove);

            // 2. Правая стена и граница камеры.
            Undo.RecordObject(right, "Widen rooms");
            right.position += Vector3.right * delta;
            Undo.RecordObject(bounds, "Widen rooms");
            var pts = bounds.points;
            float maxLocal = float.MinValue;
            foreach (var p in pts) maxLocal = Mathf.Max(maxLocal, p.x);
            for (int i = 0; i < pts.Length; i++)
                if (pts[i].x >= maxLocal - 0.01f) pts[i].x += delta / bounds.transform.lossyScale.x;
            bounds.points = pts;

            foreach (var t in toMove)
            {
                Undo.RecordObject(t, "Widen rooms");
                t.position += Vector3.right * delta;
            }

            // 3. Серый фон: полосы пола, стены, потолка — на всю новую ширину.
            var backdrop = room.transform.Find("~GrayBackdrop");
            if (backdrop != null)
                foreach (Transform strip in backdrop)
                {
                    var sr = strip.GetComponent<SpriteRenderer>();
                    if (sr == null || sr.sprite == null) continue;
                    var b = sr.bounds;
                    float newMin = b.min.x, newMax = b.max.x + delta;
                    Undo.RecordObject(strip, "Widen rooms");
                    strip.position = new Vector3((newMin + newMax) / 2f, strip.position.y, strip.position.z);
                    var s = strip.localScale;
                    strip.localScale = new Vector3(s.x * (newMax - newMin) / b.size.x, s.y, s.z);
                }

            // 4. Лампы: тот же рисунок света повторяется через каждую исходную ширину комнаты.
            int lamps = 0;
            if (repeatLights) lamps = RepeatLights(room.transform, minX, maxX, delta);

            return $"{roomName}: ширина {TargetWidth:0.#} (+{delta:0.#})" + (lamps > 0 ? $", ламп добавлено: {lamps}." : ".");
        }

        /// <summary>Граница камеры по высоте не меньше ±MinHalfHeight от центра комнаты. true — если пришлось поднять.</summary>
        static bool EnsureHeight(PolygonCollider2D bounds)
        {
            var pts = bounds.points;
            float scale = Mathf.Abs(bounds.transform.lossyScale.y);
            float need = MinHalfHeight / (scale > 0f ? scale : 1f);
            bool changed = false;
            for (int i = 0; i < pts.Length; i++)
                if (Mathf.Abs(pts[i].y) < need - 0.001f)
                {
                    pts[i].y = Mathf.Sign(pts[i].y == 0f ? 1f : pts[i].y) * need;
                    changed = true;
                }
            if (!changed) return false;
            Undo.RecordObject(bounds, "Widen rooms");
            bounds.points = pts;
            return true;
        }

        static int InvalidateConfiners(PolygonCollider2D bounds)
        {
            int n = 0;
            foreach (var c in Object.FindObjectsByType<CinemachineConfiner2D>(FindObjectsInactive.Include))
                if (c.BoundingShape2D == bounds)
                {
                    c.InvalidateBoundingShapeCache();
                    n++;
                }
            return n;
        }

        static void Collect(Transform t, float rightWall, List<Transform> toMove)
        {
            // Стены, фон, границы и свет двигаются отдельно.
            if (t.name is "Walls" or "~GrayBackdrop" or "CameraBounds" or "Background" or "Lights" or "~LightsRepeat") return;
            // Группы (Props и т. п.) — смотрим внутрь; объекты — по положению.
            if (t.childCount > 0 && t.GetComponent<Renderer>() == null && t.GetComponent<Collider2D>() == null
                && t.GetComponent<MonoBehaviour>() == null)
            {
                foreach (Transform c in t) Collect(c, rightWall, toMove);
                return;
            }
            if (t.position.x > rightWall - RightZone) toMove.Add(t);
        }

        static int RepeatLights(Transform room, float minX, float maxX, float delta)
        {
            var lights = room.Find("Lights");
            if (lights == null) return 0;
            var old = room.Find("~LightsRepeat");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var repeat = new GameObject("~LightsRepeat");
            Undo.RegisterCreatedObjectUndo(repeat, "Widen rooms");
            repeat.transform.SetParent(room, false);

            float period = maxX - minX;
            float newMax = maxX + delta - 1f;
            int count = 0;
            for (float shift = period; ; shift += period)
            {
                bool any = false;
                foreach (Transform l in lights)
                {
                    if (l.position.x + shift > newMax) continue;
                    var copy = Object.Instantiate(l.gameObject, repeat.transform, true);
                    copy.name = l.name;
                    copy.transform.position = l.position + Vector3.right * shift;
                    count++;
                    any = true;
                }
                if (!any) break;
            }
            return count;
        }

        static bool EnsureScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }
    }
}
