using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Overworked
{
    /// <summary>
    /// Сохранение на диск: Continue после перезапуска игры продолжает ровно с того места, где остановились.
    /// В файл (Application.persistentDataPath/overworked_save.json) пишется:
    /// — состояние игры: имя, этаж, флаги, задания (выполненные и активные), история телефона, хотбар;
    /// — снимок этажа: позиция и комната Дэнни, рассудок, какие объекты сцены включены/выключены/уничтожены
    ///   относительно исходной сцены и одноразовые флажки компонентов (сценарий отыгран, триггер сработал,
    ///   предмет использован, дверь заперта, фонарик подобран/включён — см. StateFields).
    /// Когда пишется: только по сюжетным чекпоинтам — при входе на этаж (без снимка — этаж с начала) и на
    /// платформе-чекпоинте (CheckpointPlatform → Checkpoint(), со снимком). Ни автосохранения, ни сохранения
    /// при выходе в меню / закрытии игры нет.
    /// Последний чекпоинт держится и в памяти: при рассудке 0 (не в WhiteRoom и не посреди сценария) этаж
    /// перезагружается с него (ReloadCheckpoint), Дэнни мигает 2 с. В памяти — и для рабочих сцен (Floor_03_Work),
    /// и при запуске прямо из редактора; на диск — только настоящие этажи после главного меню.
    /// Continue (GameManager.ContinueFromSave): этаж грузится, снимок применяется до Start объектов сцены —
    /// отыгранные сценарии (в том числе стартовые) не повторяются; через кадр Дэнни встаёт на место.
    /// New Game и конец игры (титры) стирают файл.
    /// </summary>
    public static class SaveSystem
    {
        const int Version = 2;
        const string FileName = "overworked_save.json";
        const float RespawnBlinkSeconds = 2f;
        const float RespawnBlinkStep = 0.08f;

        // Одноразовые флажки компонентов этажа (поля и автосвойства). Сохраняются у всех компонентов Overworked.
        static readonly HashSet<string> StateFields = new HashSet<string>
        {
            "used", "locked", "doorLocked", "fired", "done", "won", "started", "onboarded", "introDone", "visited",
            // Турникет: своя карта уже отказала (иначе после Continue снова «Use your card» при пропуске Макса);
            // описание при подходе и полные реплики «No return» — только в первый раз.
            "denied", "shown", "shownOnce",
            "<HasPlayed>k__BackingField", "<SanityApplied>k__BackingField", "<HasHit>k__BackingField",
            "<Available>k__BackingField", "<IsOn>k__BackingField", "<Visited>k__BackingField",
        };

        [Serializable] class ActiveEntry { public string path; public bool active; }
        [Serializable] class FieldEntry { public string path; public string type; public string field; public bool value; }

        [Serializable]
        class FloorState
        {
            public string scene;
            public float x, y;
            public int facing = 1;
            public string room;
            public float sanity = -1f;
            public List<ActiveEntry> actives = new List<ActiveEntry>();
            public List<string> destroyed = new List<string>();
            public List<FieldEntry> fields = new List<FieldEntry>();
        }

        [Serializable]
        class SaveData
        {
            public int version;
            public string scene;   // сцена чекпоинта (для перезагрузки при рассудке 0)
            public string playerName;
            public int floor;
            public List<string> flags = new List<string>();
            public List<string> completedObjectives = new List<string>();
            public List<string> activeObjectives = new List<string>();
            public List<string> messages = new List<string>();
            public List<string> unreadMessages = new List<string>();
            public List<string> hotbar = new List<string>();   // id предмета по слотам, "" — пусто
            public bool hasFloorState;
            public FloorState floorState = new FloorState();
        }

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Есть ли на диске сохранение, которое можно загрузить (файл есть и версия та же).</summary>
        public static bool HasSave
        {
            get
            {
                try
                {
                    if (!File.Exists(FilePath)) return false;
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    return data != null && data.version == Version;
                }
                catch { return false; }
            }
        }

        // Поля-флажки по типу компонента: отражение один раз на тип, а не каждое автосохранение.
        static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();

        static FieldInfo[] StateFieldsOf(Type t)
        {
            if (fieldCache.TryGetValue(t, out var cached)) return cached;
            var list = new List<FieldInfo>();
            if (t.Namespace == "Overworked")
                for (var type = t; type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                    foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (f.FieldType == typeof(bool) && StateFields.Contains(f.Name)) list.Add(f);
            return fieldCache[t] = list.ToArray();
        }

        // Игра в этой сессии прошла через главное меню (New Game / Continue). Этаж, запущенный прямо из редактора
        // (или прыжками F2–F5 без меню), ничего не пишет — тестовый прогон не затрёт настоящее сохранение.
        static bool menuSeen;

        // Исходная сцена этажа: какие объекты были включены сразу после загрузки (до Start) — с ней сравнивается снимок.
        static Scene baselineScene;
        static Dictionary<string, bool> baseline;

        // Снимок этажа из файла, который надо применить, когда этот этаж загрузится (Continue).
        static FloorState pending;

        // Последний чекпоинт (JSON SaveData) — с него перезагрузка при рассудке 0.
        static string lastCheckpoint;
        // Идёт перезагрузка с чекпоинта: не запускать вторую, после загрузки — мигание Дэнни.
        static bool reloading;

        static Runner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            menuSeen = false;   // Enter Play Mode без перезагрузки домена — статика не сбрасывается сама
            pending = null;
            baseline = null;
            lastCheckpoint = null;
            reloading = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (runner == null)
            {
                var go = new GameObject("SaveSystem");
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
        }

        // Первая сцена Play Mode (запуск прямо с этажа в редакторе): sceneLoaded для неё может не прийти —
        // тогда разбираем её здесь (после Awake, до Start), иначе чекпоинты на ней не работают.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (baseline == null || baselineScene != scene) OnSceneLoaded(scene, LoadSceneMode.Single);
        }

        // ------------------------------------------------------------ загрузка сцен

        // До Start сцены: объекты проснулись, но сценарии ещё ничего не запустили.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu") { menuSeen = true; return; }
            if (mode != LoadSceneMode.Single) return;
            int floor = FloorOf(scene.name);
            if (floor < 0) floor = WorkFloorOf(scene.name);
            if (floor < 0) { baseline = null; return; }

            if (reloading)
            {
                reloading = false;
                // Рассудок не остаётся 0 (иначе снова перезагрузка): FloorSettings/снимок потом выставят свой.
                if (SanityManager.Instance != null && SanityManager.Instance.Current <= 0f) SanityManager.Instance.SetSanityInstant(100f);
                runner.StartCoroutine(BlinkAfterFadeIn());
            }

            baselineScene = scene;
            baseline = new Dictionary<string, bool>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    baseline[PathOf(t)] = t.gameObject.activeSelf;

            if (pending != null && pending.scene == scene.name)
            {
                var state = pending;
                pending = null;
                ApplyFloorState(scene, state);
                runner.StartCoroutine(ApplyAfterStart(state));
                return;   // файл уже содержит этот снимок — не затираем его чекпоинтом «с начала»
            }
            pending = null;
            // Вход на этаж — сюжетный чекпоинт «этаж с начала».
            if (GameManager.Instance != null) Write(floor, null, scene.name);
        }

        static int FloorOf(string sceneName)
        {
            if (sceneName == "WhiteRoom") return 0;
            if (sceneName.StartsWith("Floor_0") && int.TryParse(sceneName.Substring(7), out int f)) return f;
            return -1;
        }

        // Рабочие сцены этажей (Floor_03_Work): чекпоинты в памяти работают, на диск не пишутся.
        static int WorkFloorOf(string sceneName)
        {
            if (sceneName.StartsWith("Floor_0") && sceneName.Length > 7 && char.IsDigit(sceneName[7])) return sceneName[7] - '0';
            return -1;
        }

        // Путь объекта в сцене с номерами среди соседей — однозначный, пока сцена та же.
        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null; c = c.parent) parts.Add(c.name + "#" + c.GetSiblingIndex());
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ------------------------------------------------------------ сохранение

        /// <summary>
        /// Идёт ли что-то, что нельзя оборвать или сохранить посередине: реплика, сценарий, галлюцинация.
        /// Тогда пауза не пускает в меню, а автосохранение ждёт.
        /// </summary>
        public static bool StoryBusy()
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying) return true;
            foreach (var s in UnityEngine.Object.FindObjectsByType<ScriptedSequence>(FindObjectsSortMode.None))
                if (s.IsPlaying) return true;
            foreach (var h in UnityEngine.Object.FindObjectsByType<HallucinationEvent>(FindObjectsSortMode.None))
                if (h.IsPlaying) return true;
            return false;
        }

        /// <summary>
        /// Чекпоинт со снимком этажа (платформа-чекпоинт). Пропускается, если мы не на этаже, Дэнни не свободен
        /// или что-то идёт — тогда false и остаётся прошлый чекпоинт. spawnX — где Дэнни появится при
        /// перезагрузке (центр платформы); null — где он стоит сейчас.
        /// </summary>
        public static bool Checkpoint(float? spawnX = null)
        {
            string why = CheckpointBlocker();
            if (why != null)
            {
                Debug.Log($"[Save] чекпоинт пропущен: {why}");
                return false;
            }
            var scene = SceneManager.GetActiveScene();
            var player = PlayerController.Instance;
            int floor = FloorOf(scene.name);
            var state = CaptureFloorState(scene, player);
            if (spawnX.HasValue) state.x = spawnX.Value;
            Write(floor >= 0 ? floor : WorkFloorOf(scene.name), state, scene.name);
            Debug.Log($"[Save] чекпоинт: {scene.name}, x {state.x:0.0}");
            return true;
        }

        // Почему чекпоинт сейчас нельзя (null — можно).
        static string CheckpointBlocker()
        {
            if (GameManager.Instance == null) return "нет GameManager";
            if (baseline == null) return "сцена не считается этажом";
            var scene = SceneManager.GetActiveScene();
            if (scene != baselineScene || !scene.isLoaded) return $"активная сцена {scene.name} — не этаж";
            var player = PlayerController.Instance;
            if (player == null) return "нет PlayerController";
            if (player.State != PlayerState.Normal) return $"Дэнни в состоянии {player.State}";
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsLoading) return "идёт переход сцены";
            if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return "пауза";
            if (StoryBusy()) return "идёт реплика/сценарий/галлюцинация";
            return null;
        }

        static FloorState CaptureFloorState(Scene scene, PlayerController player)
        {
            var st = new FloorState { scene = scene.name };
            var p = player.transform.position;
            st.x = p.x; st.y = p.y;
            st.facing = player.Facing;
            st.room = Room.Current != null ? Room.Current.name : null;
            if (SanityManager.Instance != null) st.sanity = SanityManager.Instance.Current;

            var seen = new HashSet<string>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    string path = PathOf(t);
                    seen.Add(path);
                    if (baseline.TryGetValue(path, out bool was) && was != t.gameObject.activeSelf)
                        st.actives.Add(new ActiveEntry { path = path, active = t.gameObject.activeSelf });
                    foreach (var mb in t.GetComponents<MonoBehaviour>())
                        CaptureFields(mb, path, st.fields);
                }
            foreach (var path in baseline.Keys)
                if (!seen.Contains(path)) st.destroyed.Add(path);
            return st;
        }

        static void CaptureFields(MonoBehaviour mb, string path, List<FieldEntry> into)
        {
            if (mb == null) return;
            foreach (var f in StateFieldsOf(mb.GetType()))
                into.Add(new FieldEntry { path = path, type = mb.GetType().Name, field = f.Name, value = (bool)f.GetValue(mb) });
        }

        // Чекпоинт: состояние игры сейчас + снимок этажа (null — «этаж с начала»). В память — всегда,
        // в файл — только настоящий этаж (не Floor_03_Work) в игре, начатой из главного меню.
        static void Write(int floor, FloorState floorState, string sceneName)
        {
            var gm = GameManager.Instance;
            var data = new SaveData
            {
                version = Version,
                scene = sceneName,
                playerName = gm.State.PlayerName,
                floor = floor,
                flags = new List<string>(gm.State.Flags),
                hasFloorState = floorState != null,
                floorState = floorState ?? new FloorState(),
            };
            var om = ObjectiveManager.Instance;
            if (om != null)
            {
                data.completedObjectives.AddRange(om.CompletedIds);
                foreach (var o in om.Active) if (o != null) data.activeObjectives.Add(o.Id);
            }
            var mm = MessageManager.Instance;
            if (mm != null)
                foreach (var m in mm.History)
                {
                    if (m == null) continue;
                    data.messages.Add(m.name);
                    if (mm.IsUnread(m)) data.unreadMessages.Add(m.name);
                }
            for (int i = 0; i < Hotbar.SlotCount; i++)
                data.hotbar.Add(Hotbar.Get(i) != null ? Hotbar.Get(i).id : "");
            string json = JsonUtility.ToJson(data);
            lastCheckpoint = json;
            if (floorState == null) Debug.Log($"[Save] чекпоинт: {sceneName} с начала");
            if (!menuSeen || FloorOf(sceneName) < 0) return;
            try
            {
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] не удалось записать {FilePath}: {e.Message}");
            }
        }

        // ------------------------------------------------------------ загрузка

        /// <summary>
        /// Прочитать сохранение и выставить по нему состояние игры (имя, флаги, задания, телефон); снимок этажа
        /// применится, когда этаж загрузится. Возвращает этаж для загрузки или -1, если сохранения нет / не читается.
        /// </summary>
        public static int Load()
        {
            SaveData data;
            string json;
            try
            {
                if (!HasSave) return -1;
                json = File.ReadAllText(FilePath);
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] не удалось прочитать {FilePath}: {e.Message}");
                return -1;
            }
            if (data == null || data.version != Version) return -1;
            if (!ApplyGameState(data)) return -1;
            lastCheckpoint = json;
            menuSeen = true;
            Debug.Log($"[Save] загружено: этаж {data.floor}, имя {GameManager.Instance.State.PlayerName}{(data.hasFloorState ? ", с места остановки" : ", с начала этажа")}");
            return data.floor;
        }

        /// <summary>
        /// Рассудок 0: этаж перезагружается с последнего чекпоинта (затемнение, после — Дэнни мигает 2 с).
        /// Чекпоинта нет — сцена с начала. Позже сюда добавится мини-анимация перед перезагрузкой.
        /// </summary>
        public static void ReloadCheckpoint()
        {
            if (reloading) return;
            var stm = SceneTransitionManager.Instance;
            if (stm == null || stm.IsLoading) return;
            var data = lastCheckpoint != null ? JsonUtility.FromJson<SaveData>(lastCheckpoint) : null;
            string scene = data != null && !string.IsNullOrEmpty(data.scene) ? data.scene : SceneManager.GetActiveScene().name;
            if (data != null) ApplyGameState(data);
            else pending = null;
            reloading = true;
            Debug.Log($"[Save] рассудок 0 → чекпоинт {scene}{(data != null && data.hasFloorState ? "" : " с начала")}");
            stm.Load(scene);
        }

        // Состояние игры из сохранения (имя, этаж, флаги, задания, телефон) + снимок этажа на загрузку сцены.
        static bool ApplyGameState(SaveData data)
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            if (!string.IsNullOrEmpty(data.playerName)) gm.State.PlayerName = data.playerName;
            gm.State.CurrentFloor = data.floor;
            gm.State.Flags = new HashSet<string>(data.flags);

            var reg = Resources.Load<SaveRegistry>(SaveRegistry.ResourcePath);
            if (reg == null) Debug.LogWarning("[Save] нет Resources/SaveRegistry — активные задания и телефон не восстановятся");

            if (ObjectiveManager.Instance != null)
            {
                var active = new List<ObjectiveData>();
                if (reg != null)
                    foreach (var id in data.activeObjectives)
                    {
                        var o = reg.Objective(id);
                        if (o != null) active.Add(o);
                        else Debug.LogWarning($"[Save] задание {id} не найдено в SaveRegistry");
                    }
                ObjectiveManager.Instance.Restore(data.completedObjectives, active);
            }

            if (MessageManager.Instance != null && reg != null)
            {
                var history = new List<MessageData>();
                var unread = new List<MessageData>();
                foreach (var n in data.messages) { var m = reg.Message(n); if (m != null) history.Add(m); }
                foreach (var n in data.unreadMessages) { var m = reg.Message(n); if (m != null) unread.Add(m); }
                MessageManager.Instance.Restore(history, unread);
            }

            // Хотбар — как в чекпоинте: подобранное после него снова лежит на месте и из хотбара убирается.
            var items = new HotbarItemData[Hotbar.SlotCount];
            for (int i = 0; i < items.Length && i < data.hotbar.Count; i++)
            {
                if (string.IsNullOrEmpty(data.hotbar[i])) continue;
                items[i] = reg != null ? reg.Item(data.hotbar[i]) : null;
                if (items[i] == null) Debug.LogWarning($"[Save] предмет {data.hotbar[i]} не найден в SaveRegistry");
            }
            Hotbar.Restore(items);

            pending = data.hasFloorState ? data.floorState : null;
            if (pending != null && string.IsNullOrEmpty(pending.scene)) pending.scene = data.scene;
            return true;
        }

        // До Start: включённость объектов и флажки — чтобы стартовые сценарии увидели «уже отыграно».
        static void ApplyFloorState(Scene scene, FloorState st)
        {
            var byPath = new Dictionary<string, Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    byPath[PathOf(t)] = t;

            foreach (var e in st.actives)
                if (byPath.TryGetValue(e.path, out var t)) t.gameObject.SetActive(e.active);
            foreach (var path in st.destroyed)
                if (byPath.TryGetValue(path, out var t)) t.gameObject.SetActive(false);

            int applied = 0;
            foreach (var e in st.fields)
            {
                if (!byPath.TryGetValue(e.path, out var t)) continue;
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                {
                    if (mb == null || mb.GetType().Name != e.type) continue;
                    foreach (var f in StateFieldsOf(mb.GetType()))
                        if (f.Name == e.field) { f.SetValue(mb, e.value); applied++; break; }
                }
            }

            // Фонарик: флажки выставлены в обход Toggle — свет приводим к IsOn сами (без щелчка и OnToggled).
            var fc = FlashlightController.Instance;
            if (fc != null)
            {
                var light = fc.GetComponentInChildren<Light2D>(true);
                if (light != null) light.enabled = fc.IsOn;
            }
            Debug.Log($"[Save] снимок этажа: объектов {st.actives.Count + st.destroyed.Count}, флажков {applied}");
        }

        // После Start (FloorSettings выставил рассудок этажа, камеры настроены): Дэнни на место, комната, рассудок.
        static IEnumerator ApplyAfterStart(FloorState st)
        {
            yield return null;
            var player = PlayerController.Instance;
            if (player != null)
            {
                player.Teleport(new Vector3(st.x, st.y, player.transform.position.z));
                player.RestoreFacing(st.facing);
            }
            if (!string.IsNullOrEmpty(st.room))
                foreach (var room in UnityEngine.Object.FindObjectsByType<Room>(FindObjectsSortMode.None))
                    if (room.name == st.room) { room.RestoreInside(); break; }
            if (st.sanity >= 0f && SanityManager.Instance != null) SanityManager.Instance.SetSanityInstant(st.sanity);

            // Камера — сразу на месте, пока экран тёмный: без перехода от стартовой камеры и без доезда за Дэнни
            // (иначе после затемнения видно, как всё «встаёт на места»).
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            var saved = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            foreach (var cam in UnityEngine.Object.FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsSortMode.None))
                cam.PreviousStateIsValid = false;
            yield return null;
            yield return null;
            if (brain != null) brain.DefaultBlend = saved;
        }

        /// <summary>Стереть сохранение (New Game, конец игры).</summary>
        public static void Delete()
        {
            pending = null;
            lastCheckpoint = null;
            try
            {
                if (HasSave) File.Delete(FilePath);
                Debug.Log("[Save] сохранение стёрто");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] не удалось стереть {FilePath}: {e.Message}");
            }
        }

        // После перезагрузки с чекпоинта: как только экран начал проявляться — Дэнни часто мигает 2 с.
        static IEnumerator BlinkAfterFadeIn()
        {
            var stm = SceneTransitionManager.Instance;
            bool fadeIn = stm == null;
            void OnFadeIn() => fadeIn = true;
            if (stm != null) stm.FadeInStarted += OnFadeIn;
            while (!fadeIn && stm != null && stm.IsLoading) yield return null;
            if (stm != null) stm.FadeInStarted -= OnFadeIn;

            var player = PlayerController.Instance;
            if (player == null) yield break;
            var renderers = new List<SpriteRenderer>();
            foreach (var r in player.GetComponentsInChildren<SpriteRenderer>())
                if (r.enabled) renderers.Add(r);
            bool visible = true;
            for (float t = 0f; t < RespawnBlinkSeconds; t += RespawnBlinkStep)
            {
                visible = !visible;
                foreach (var r in renderers) if (r != null) r.enabled = visible;
                yield return new WaitForSecondsRealtime(RespawnBlinkStep);
            }
            foreach (var r in renderers) if (r != null) r.enabled = true;
        }

        // Рассудок 0 на этаже → перезагрузка с чекпоинта. Не в WhiteRoom (там 0 — концовка) и не посреди
        // сценария/галлюцинации (сюжетный 0 — дождёмся конца).
        class Runner : MonoBehaviour
        {
            void Update()
            {
                var sanity = SanityManager.Instance;
                if (reloading || sanity == null || sanity.Current > 0f || baseline == null) return;
                var scene = SceneManager.GetActiveScene();
                if (scene != baselineScene || scene.name == "WhiteRoom") return;
                if (SceneTransitionManager.Instance == null || SceneTransitionManager.Instance.IsLoading) return;
                if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return;
                if (StoryBusy()) return;
                ReloadCheckpoint();
            }
        }
    }
}
