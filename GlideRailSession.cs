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

            // Śledź request żeby zawsze móc go zwolnić
            _cursorRequested = true;
            CursorManager.Request();
        }

        public void OnPanelClosed()
        {
            if (!_flyActive) return;

            StopPlayback();
            ReturnControlToPlayer();
        }

        private void ReturnControlToPlayer()
        {
            // Najpierw sygnalizuj że już nie lecimy
            _flyActive = false;
            _playing = false;
            _uiMode = false;

            // Teraz Release — OnCursorHide zobaczy IsFlyActive=false → ustawi GameMode.Garage
            if (_cursorRequested)
            {
                _cursorRequested = false;
                CursorManager.Release();
            }

            RestoreBrain();
            SetPlayerInput(true);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CURSOR TOGGLE (F9)
        // ═════════════════════════════════════════════════════════════════════

        public void ToggleCursor()
        {
            if (!_flyActive) return;
            _uiMode = !_uiMode;

            if (_uiMode)
            {
                // Fly → UI: pokaż kursor
                _cursorRequested = true;
                CursorManager.Request();
            }
            else
            {
                // UI → Fly: schowaj kursor
                // OnCursorHide NIE przełączy GameMode bo IsFlyActive=true
                if (_cursorRequested)
                {
                    _cursorRequested = false;
                    CursorManager.Release();
                }
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
            bool fast = Input.GetKey(KeyCode.LeftShift)
                      || Input.GetKey(KeyCode.RightShift);
            float spd = _flySpeed * dt * (fast ? 3.5f : 1f);
            var tf = _mainCam.transform;

            if (Input.GetKey(KeyCode.W)) tf.position += tf.forward * spd;
            if (Input.GetKey(KeyCode.S)) tf.position -= tf.forward * spd;
            if (Input.GetKey(KeyCode.A)) tf.position -= tf.right * spd;
            if (Input.GetKey(KeyCode.D)) tf.position += tf.right * spd;

            // Space/Ctrl — ruch pionowy, też skalowany przez _flySpeed
            // (użytkownik chciał żeby sens wpływał też na te osie —
            //  mnożymy przez _flySens jako drugi wymiar kontroli)
            float vertSpd = spd * _flySens;
            if (Input.GetKey(KeyCode.Space)) tf.position += Vector3.up * vertSpd;
            if (Input.GetKey(KeyCode.LeftControl)
             || Input.GetKey(KeyCode.RightControl)) tf.position -= Vector3.up * vertSpd;
        }

        // ═════════════════════════════════════════════════════════════════════
        // PLAYBACK
        // ═════════════════════════════════════════════════════════════════════

        private void TickPlayback(float dt)
        {
            _playT += dt;
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
            try
            {
                var piType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t =>
                        t.FullName == "Il2CppCMS.Player.Controller.PlayerInput");

                if (piType == null) return;

                var raw = UnityEngine.Object.FindObjectOfType(
                    Il2CppInterop.Runtime.Il2CppType.From(piType));
                var comp = raw?.TryCast<UnityEngine.Behaviour>();
                if (comp != null) comp.enabled = enabled;
            }
            catch (Exception ex)
            {
                GlideRailPlugin.Log.Warning(
                    $"[GlideRail] SetPlayerInput({enabled}): {ex.Message}");
            }
        }
    }
}