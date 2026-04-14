using CMS2026UITKFramework;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GlideRail
{
    public class GlideRailSession
    {
        // ── Nowe pole ─────────────────────────────────────────────────────────────
        private GameObject _cursorGO;

        // ── Kamera ────────────────────────────────────────────────────────────
        private GameObject _mainCam;
        private UnityEngine.Behaviour _brain;

        // ── Stan ──────────────────────────────────────────────────────────────
        private bool _flyActive = false;
        private bool _uiMode = false;
        private float _camYaw = 0f;
        private float _camPitch = 0f;
        private float _camRoll = 0f;
        private float _flySpeed = 14f;
        private float _flySens = 1.8f;
        private bool _cursorRequested = false;

        // ── Ścieżka ───────────────────────────────────────────────────────────
        private readonly List<GlideKeyframe> _keyframes = new();

        // ── Playback ──────────────────────────────────────────────────────────
        private bool _playing = false;
        private float _playT = 0f;
        private float _playDur = 12f;

        // ── Sub-systemy ───────────────────────────────────────────────────────
        private GlideDebugRenderer _debugRenderer;
        private GlideRailPanel _panel;

        // ═════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═════════════════════════════════════════════════════════════════════

        // ── Pomocnicze metody kursora ─────────────────────────────────────────────
        private void SetCursorVisible(bool visible)
        {
            if (_cursorGO == null)
                _cursorGO = GameObject.Find("!!UI/CanvasMouse/Cursor");
            if (_cursorGO != null)
                _cursorGO.SetActive(visible);
        }

        public void Initialize()
        {
            _mainCam = GameObject.Find("MainCamera");

            _debugRenderer = new GlideDebugRenderer();
            _debugRenderer.Initialize();

            _panel = new GlideRailPanel(this);
            _panel.Build();  // panel ukryty

            GlideRailPlugin.Log.Msg("[GlideRail] Session initialized.");
        }

        public void Shutdown()
        {
            if (_flyActive)
                ReturnControlToPlayer();

            _panel?.Destroy();
            _panel = null;

            _debugRenderer?.Shutdown();
            _debugRenderer = null;

            GlideRailPlugin.Log.Msg("[GlideRail] Session shutdown.");
        }

        public void OnUpdate()
        {
            if (!_flyActive) return;

            // Wymuszaj GameMode.UI co klatkę
            try
            {
                if (Il2CppCMS.Core.GameMode.Get().currentMode != Il2Cpp.gameMode.UI)
                    Il2CppCMS.Core.GameMode.Get().SetCurrentMode(Il2Cpp.gameMode.UI);
            }
            catch { }

            // W Fly Mode wymuszaj niewidoczność kursora co klatkę
            // — gra może go przywracać przez Cursor3D.ShowCursor()
            if (!_uiMode)
                SetCursorVisible(false);

            HandleHotkeys();

            if (_playing)
            {
                TickPlayback(Time.deltaTime);
                return;
            }

            if (!_uiMode)
                TickFlyControls(Time.deltaTime);
        }

        // ═════════════════════════════════════════════════════════════════════
        // PANEL OPEN / CLOSE
        // ═════════════════════════════════════════════════════════════════════

        public void OnPanelOpened()
        {
            if (_flyActive) return;

            _mainCam = GameObject.Find("MainCamera");
            SyncAnglesFromCamera();
            FindAndDisableBrain();
            SetPlayerInput(false);

            _flyActive = true;
            _uiMode = true;

            Il2CppCMS.Core.GameMode.Get().SetCurrentMode(Il2Cpp.gameMode.UI);
            SetCursorVisible(true);
            _panel?.OnCursorModeChanged(_uiMode);
        }

        public void OnPanelClosed()
        {
            if (!_flyActive) return;

            StopPlayback();
            ReturnControlToPlayer();
        }

        private void ReturnControlToPlayer()
        {
            _flyActive = false;
            _playing = false;
            _uiMode = false;

            Il2CppCMS.Core.GameMode.Get().SetCurrentMode(Il2Cpp.gameMode.Garage);
            SetCursorVisible(true);
            SetPlayerInput(true);
            RestoreBrain();
        }

        // ═════════════════════════════════════════════════════════════════════
        // CURSOR TOGGLE (F9)
        // ═════════════════════════════════════════════════════════════════════

        public void ToggleCursor()
        {
            if (!_flyActive) return;
            _uiMode = !_uiMode;

            // GameMode.UI zostaje zawsze — tylko kursor i input się zmieniają
            if (_uiMode)
            {
                SetPlayerInput(false);
                SetCursorVisible(true);
            }
            else
            {
                SetPlayerInput(true);
                SetCursorVisible(false);
            }

            _panel?.OnCursorModeChanged(_uiMode);
        }

        // ═════════════════════════════════════════════════════════════════════
        // HOTKEYS
        // ═════════════════════════════════════════════════════════════════════

        private void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.F5)) AddKeyframe();
            if (Input.GetKeyDown(KeyCode.F6)) RemoveLastKeyframe();
            if (Input.GetKeyDown(KeyCode.F9)) ToggleCursor();

            // Zawsze gdy panel żyje — ostrzeż o zablokowanych klawiszach
            CheckBlockedKeys();
        }

        private static readonly KeyCode[] FLY_ALLOWED = new[]
{
    KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
    KeyCode.Q, KeyCode.E,
    KeyCode.Space, KeyCode.LeftControl, KeyCode.RightControl,
    KeyCode.LeftShift, KeyCode.RightShift,
    KeyCode.LeftAlt,
    KeyCode.F5, KeyCode.F6, KeyCode.F9,
    KeyCode.Mouse0  // ← lewy przycisk myszy
};
        private float _hintCooldown = 0f;


        private void CheckBlockedKeys()
        {
            _hintCooldown -= Time.deltaTime;

            // Nie spamuj gdy konsola jest otwarta
            if (IsConsoleVisible()) return;

            // Sprawdź mysz — tylko lewy przycisk dozwolony
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                ShowBlockedHint();
                return;
            }

            foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(kc)) continue;
                if (System.Array.IndexOf(FLY_ALLOWED, kc) >= 0) continue;
                ShowBlockedHint();
                return;
            }
        }

        private static bool IsConsoleVisible()
        {
            try
            {
                var apiType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == "CMS2026SimpleConsole.ConsolePlugin");

                if (apiType == null) return false;

                var comp = apiType.GetProperty("ConsoleComponent",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static)?.GetValue(null);

                if (comp == null) return false;

                var renderer = comp.GetType()
                    .GetProperty("Renderer")?.GetValue(comp);

                return (bool)(renderer?.GetType()
                    .GetProperty("IsVisible")?.GetValue(renderer) ?? false);
            }
            catch { return false; }
        }


        private void ShowBlockedHint()
        {
            if (_hintCooldown > 0f) return;
            _hintCooldown = 3f;

            try
            {
                Il2CppCMS.UI.UIManager.Get().ShowPopup(
                    "<color=#ff4040>Exit GlideRail to use game functions</color>",
                    Il2Cpp.PopupType.Normal);
            }
            catch { }
        }

        // ═════════════════════════════════════════════════════════════════════
        // FLY CONTROLS
        // ═════════════════════════════════════════════════════════════════════

        private void TickFlyControls(float dt)
        {
            if (_mainCam == null) return;

            // Mouse look
            _camYaw += Input.GetAxis("Mouse X") * _flySens;
            _camPitch = Mathf.Clamp(
                _camPitch - Input.GetAxis("Mouse Y") * _flySens, -88f, 88f);

            // Q/E roll — skalowany przez _flySens tak samo jak mysz
            if (Input.GetKey(KeyCode.Q)) _camRoll += 55f * dt * _flySens;
            if (Input.GetKey(KeyCode.E)) _camRoll -= 55f * dt * _flySens;

            _mainCam.transform.rotation =
                Quaternion.Euler(_camPitch, _camYaw, _camRoll);

            // WASD / Space / Ctrl — skalowane przez _flySpeed
            bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float spd = _flySpeed * dt * _flySens * (fast ? 3.5f : 1f);  
            var tf = _mainCam.transform;

            if (Input.GetKey(KeyCode.W)) tf.position += tf.forward * spd;
            if (Input.GetKey(KeyCode.S)) tf.position -= tf.forward * spd;
            if (Input.GetKey(KeyCode.A)) tf.position -= tf.right * spd;
            if (Input.GetKey(KeyCode.D)) tf.position += tf.right * spd;

            if (Input.GetKey(KeyCode.Q)) _camRoll += 55f * dt * _flySens;
            if (Input.GetKey(KeyCode.E)) _camRoll -= 55f * dt * _flySens;

            float vertSpd = spd;  // już zawiera _flySens
            if (Input.GetKey(KeyCode.Space)) tf.position += Vector3.up * vertSpd;
            if (Input.GetKey(KeyCode.LeftControl)
             || Input.GetKey(KeyCode.RightControl)) tf.position -= Vector3.up * vertSpd;
        }

        // ═════════════════════════════════════════════════════════════════════
        // PLAYBACK
        // ═════════════════════════════════════════════════════════════════════

        private void TickPlayback(float dt)
        {
            float speed = GlideSpline.SampleSpeed(_keyframes,
                Mathf.Clamp01(_playT / _playDur));
            _playT += dt * speed;  // ← mnoży dt przez speed z KF

            float nt = Mathf.Clamp01(_playT / _playDur);
            var (pos, rot) = GlideSpline.Sample(_keyframes, nt);

            if (_mainCam != null)
            {
                _mainCam.transform.position = pos;
                _mainCam.transform.rotation = rot;
                SyncAnglesFromCamera();
            }

            _panel?.OnPlaybackTick(_playT, _playDur);

            if (nt >= 1f)
            {
                _playing = false;
                _debugRenderer?.SetVisible(true);
                _panel?.OnPlaybackEnd();
            }
        }

        public void StartPlayback()
        {
            if (_keyframes.Count < 2) return;
            _playing = true;
            _playT = 0f;
            _debugRenderer?.SetVisible(false);
        }

        public void StopPlayback()
        {
            _playing = false;
            _debugRenderer?.SetVisible(true);
        }

        // ═════════════════════════════════════════════════════════════════════
        // KEYFRAME API
        // ═════════════════════════════════════════════════════════════════════
        public void SetKeyframeSpeed(int index, float speed)
        {
            if (index < 0 || index >= _keyframes.Count) return;
            _keyframes[index].SpeedMultiplier = speed;
        }

        public void ReplaceKeyframe(int index)
        {
            if (_mainCam == null || index < 0 || index >= _keyframes.Count) return;
            _keyframes[index].Position = _mainCam.transform.position;
            _keyframes[index].Rotation = _mainCam.transform.rotation;
            RefreshDebugVisuals();
            GlideRailPlugin.Log.Msg($"[GlideRail] KF #{index + 1} replaced");
        }


        public void AddKeyframe()
        {
            if (_mainCam == null) return;
            _keyframes.Add(new GlideKeyframe
            {
                Position = _mainCam.transform.position,
                Rotation = _mainCam.transform.rotation,
                SpeedMultiplier = 1f
            });
            RefreshDebugVisuals();
            _panel?.OnKeyframesChanged();
            GlideRailPlugin.Log.Msg(
                $"[GlideRail] KF #{_keyframes.Count} added " +
                $"({_mainCam.transform.position.x:F1}, " +
                $"{_mainCam.transform.position.y:F1}, " +
                $"{_mainCam.transform.position.z:F1})");
        }

        public void RemoveLastKeyframe()
        {
            if (_keyframes.Count == 0) return;
            _keyframes.RemoveAt(_keyframes.Count - 1);
            RefreshDebugVisuals();
            _panel?.OnKeyframesChanged();
        }

        public void RemoveKeyframe(int index)
        {
            if (index < 0 || index >= _keyframes.Count) return;
            _keyframes.RemoveAt(index);
            RefreshDebugVisuals();
            _panel?.OnKeyframesChanged();
        }

        public void JumpToKeyframe(int index)
        {
            if (_mainCam == null || index < 0 || index >= _keyframes.Count) return;
            _mainCam.transform.position = _keyframes[index].Position;
            _mainCam.transform.rotation = _keyframes[index].Rotation;
            SyncAnglesFromCamera();
        }

        public void ClearKeyframes()
        {
            _keyframes.Clear();
            RefreshDebugVisuals();
            _panel?.OnKeyframesChanged();
        }

        public void TogglePanel() => _panel?.Toggle();

        // ═════════════════════════════════════════════════════════════════════
        // EXPORT / IMPORT
        // ═════════════════════════════════════════════════════════════════════

        public string ExportPath()
        {
            if (_keyframes.Count == 0) return null;
            return GlidePathSerializer.Serialize(_keyframes, _playDur);
        }

        public void PlayExported(string data, Action<string> onError)
        {
            var kfs = GlidePathSerializer.Deserialize(data, out float dur);
            if (kfs == null || kfs.Count < 2)
            {
                onError("Failed to parse path data.");
                return;
            }
            _keyframes.Clear();
            _keyframes.AddRange(kfs);
            _playDur = dur;
            RefreshDebugVisuals();
            _panel?.OnKeyframesChanged();
            StartPlayback();
        }

        // ═════════════════════════════════════════════════════════════════════
        // STATUS
        // ═════════════════════════════════════════════════════════════════════

        public void PrintStatus(Action<string> print)
        {
            string brainState = _brain == null
                ? "none"
                : (_brain.enabled ? "enabled" : "DISABLED");

            print("═══ GlideRail Status ═══════════════");
            print($"  Fly active  : {_flyActive}");
            print($"  UI mode     : {_uiMode}");
            print($"  Keyframes   : {_keyframes.Count}");
            print($"  Playing     : {_playing}");
            print($"  Play time   : {_playT:F1}s / {_playDur:F0}s");
            print($"  Move speed  : {_flySpeed}");
            print($"  Look sens   : {_flySens:F1}");
            print($"  Camera      : {(_mainCam != null ? _mainCam.name : "null")}");
            print($"  Brain       : {brainState}");
            print("════════════════════════════════════");
        }

        /// <summary>
        /// Wołane przez GlideRailPanel po każdym RebuildPanel().
        /// Gwarantuje że kursor i hint są w sync ze stanem sesji,
        /// nawet jeśli DestroyPanel() zwolnił cursor request.
        /// </summary>
        public void NotifyPanelRebuilt()
        {
            if (!_flyActive) return;

            // Przywróć cursor request jeśli został zwolniony przez DestroyPanel
            if (_uiMode && !_cursorRequested)
            {
                _cursorRequested = true;
                CursorManager.Request();
            }

            // Sync UI (hint + przycisk) z aktualnym stanem sesji
            _panel?.OnCursorModeChanged(_uiMode);
        }


        public void ResyncCursorAfterRebuild()
        {
            if (!_flyActive) return;

            if (_uiMode)
            {
                // Upewnij się że kursor jest — niezależnie od tego co zrobił DestroyPanel
                _cursorRequested = true;
                CursorManager.Request();
            }
            // Fly mode — nie rób nic, kursor i tak nie powinien być widoczny
        }


        // ═════════════════════════════════════════════════════════════════════
        // GETTERS / SETTERS
        // ═════════════════════════════════════════════════════════════════════

        public IReadOnlyList<GlideKeyframe> Keyframes => _keyframes;
        public bool IsPlaying => _playing;
        public bool IsFlyActive => _flyActive;
        public bool IsUIMode => _uiMode;
        public float PlaybackTime => _playT;

        public float PlayDur
        {
            get => _playDur;
            set => _playDur = Mathf.Clamp(value, 2f, 180f);
        }

        public float FlySpeed
        {
            get => _flySpeed;
            set => _flySpeed = Mathf.Clamp(value, 1f, 80f);
        }

        public float FlySens
        {
            get => _flySens;
            set => _flySens = Mathf.Clamp(value, 0.1f, 10f);
        }

        // ═════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private void RefreshDebugVisuals()
            => _debugRenderer?.Refresh(_keyframes);

        private void SyncAnglesFromCamera()
        {
            if (_mainCam == null) return;
            var eu = _mainCam.transform.eulerAngles;
            _camYaw = eu.y;
            _camPitch = eu.x > 180f ? eu.x - 360f : eu.x;
            _camRoll = eu.z > 180f ? eu.z - 360f : eu.z;
        }

        private void FindAndDisableBrain()
        {
            try
            {
                var bt = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t =>
                        t.FullName == "Unity.Cinemachine.CinemachineBrain");

                if (bt == null) return;

                var raw = UnityEngine.Object.FindObjectOfType(
                    Il2CppInterop.Runtime.Il2CppType.From(bt));

                _brain = raw?.TryCast<UnityEngine.Behaviour>();
                if (_brain != null) _brain.enabled = false;
            }
            catch (Exception ex)
            {
                GlideRailPlugin.Log.Warning(
                    $"[GlideRail] FindAndDisableBrain: {ex.Message}");
            }
        }

        private void RestoreBrain()
        {
            if (_brain != null) _brain.enabled = true;
        }

        private static void SetPlayerInput(bool enabled)
        {
            // ── PlayerInput component ─────────────────────────────────────────────
            try
            {
                var piType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == "Il2CppCMS.Player.Controller.PlayerInput");

                if (piType != null)
                {
                    var raw = UnityEngine.Object.FindObjectOfType(
                        Il2CppInterop.Runtime.Il2CppType.From(piType));
                    var comp = raw?.TryCast<UnityEngine.Behaviour>();
                    if (comp != null) comp.enabled = enabled;
                }
            }
            catch (Exception ex)
            {
                GlideRailPlugin.Log.Warning($"[GlideRail] SetPlayerInput({enabled}): {ex.Message}");
            }

            // ── InputActionAsset — Gameplay + UI Common ───────────────────────────
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Unity.InputSystem");
                if (asm == null) return;

                var assetType = asm.GetType("UnityEngine.InputSystem.InputActionAsset");
                var il2Type = Il2CppInterop.Runtime.Il2CppType.From(assetType);
                var assets = UnityEngine.Resources.FindObjectsOfTypeAll(il2Type);

                // Blokujemy mapy w assets[0] — główny InputActions gry
                if (assets.Length == 0) return;
                var asset = Activator.CreateInstance(assetType, new object[] { assets[0].Pointer });
                var maps = assetType.GetProperty("actionMaps").GetValue(asset);
                int count = (int)maps.GetType().GetProperty("Count").GetValue(maps);
                var indexer = maps.GetType().GetProperty("Item");

                var toBlock = new HashSet<string> { "Gameplay", "UI Common" };

                for (int i = 0; i < count; i++)
                {
                    var map = indexer.GetValue(maps, new object[] { i });
                    var name = (string)map.GetType().GetProperty("name").GetValue(map);
                    if (!toBlock.Contains(name)) continue;
                    map.GetType().GetMethod(enabled ? "Enable" : "Disable").Invoke(map, null);
                }
            }
            catch (Exception ex)
            {
                GlideRailPlugin.Log.Warning($"[GlideRail] InputActionMaps({enabled}): {ex.Message}");
            }
        }
    }
}