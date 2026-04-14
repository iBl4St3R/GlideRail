using CMS2026UITKFramework;
using MelonLoader;
using System;
using System.Linq;
using UnityEngine;

[assembly: MelonInfo(typeof(GlideRail.GlideRailPlugin),
    "GlideRail", "0.1.0", "Blaster")]
[assembly: MelonGame("Red Dot Games", "Car Mechanic Simulator 2026 Demo")]
[assembly: MelonGame("Red Dot Games", "Car Mechanic Simulator 2026")]

namespace GlideRail
{
    public class GlideRailPlugin : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<GlideRailPlugin>.Logger;

        private GlideRailSession _session;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!FrameworkAPI.IsReady) return;
            if (!sceneName.ToLower().Contains("garage")) return;

            // CursorManager już nie potrzebny — GlideRail zarządza kursorem bezpośrednio

            _session?.Shutdown();
            _session = new GlideRailSession();
            _session.Initialize();

            TryRegisterConsole();
        }

        private static void OnCursorShow()
        {
            try
            {
                if (Il2CppCMS.Core.GameMode.Get().currentMode != Il2Cpp.gameMode.UI)
                    Il2CppCMS.Core.GameMode.Get().SetCurrentMode(Il2Cpp.gameMode.UI);
            }
            catch { }
        }

        private void OnCursorHide()
        {
            try
            {
                // Kursor potrzebny tylko gdy GlideRail leci I jest w UI mode.
                // Gdy leci w Fly mode — pozwól przywrócić GameMode.Garage (chowamy kursor).
                if (_session != null && _session.IsFlyActive && _session.IsUIMode) return;

                if (Il2CppCMS.Core.GameMode.Get().currentMode == Il2Cpp.gameMode.UI)
                    Il2CppCMS.Core.GameMode.Get().SetCurrentMode(Il2Cpp.gameMode.Garage);
            }
            catch { }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            _session?.Shutdown();
            _session = null;
        }

        public override void OnUpdate()
        {
            _session?.OnUpdate();
        }

        // ── F8 globalny toggle ──────────────────────────────────────────────
        // Możemy to przenieść do sesji, tutaj jako fallback hotkey
        // (docelowo konfigurowalny)

        private void TryRegisterConsole()
        {
            try
            {
                var apiType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.FullName == "CMS2026SimpleConsole.ConsoleAPI");

                if (apiType == null) return;

                var register = apiType.GetMethod("RegisterCommand");
                var printMeth = apiType.GetMethod("Print",
                    new[] { typeof(string), typeof(string) });

                void Print(string msg) =>
                    printMeth?.Invoke(null, new object[] { msg, "GlideRail" });

                // ── gliderail_open ──────────────────────────────────────────
                register?.Invoke(null, new object[]
                {
                    "gliderail_open",
                    "Open / close the GlideRail panel",
                    (Action<string[]>)(_ => _session?.TogglePanel())
                });

                // ── GlideRailPlay [data] ─────────────────────────────────────
                // Odtwarzanie ścieżki bez zainstalowanego moda
                register?.Invoke(null, new object[]
                {
                    "GlideRailPlay",
                    "GlideRailPlay <base64_path> — play exported camera path",
                    (Action<string[]>)(args =>
                    {
                        if (args.Length < 2)
                        {
                            Print("Usage: GlideRailPlay <exported_path_data>");
                            return;
                        }
                        string data = string.Join(" ", args, 1, args.Length - 1);
                        _session?.PlayExported(data, msg => Print(msg));
                    })
                });

                // ── gliderail_status ────────────────────────────────────────
                register?.Invoke(null, new object[]
                {
                    "gliderail_status",
                    "Show current GlideRail session info",
                    (Action<string[]>)(_ =>
                    {
                        if (_session == null)
                        {
                            Print("No active session.");
                            return;
                        }
                        _session.PrintStatus(msg => Print(msg));
                    })
                });

                // ── gliderail_export ────────────────────────────────────────
                register?.Invoke(null, new object[]
                {
                    "gliderail_export",
                    "Export current path as GlideRailPlay command",
                    (Action<string[]>)(_ =>
                    {
                        if (_session == null) { Print("No active session."); return; }
                        string exported = _session.ExportPath();
                        if (exported == null) { Print("No keyframes to export."); return; }
                        Print($"GlideRailPlay {exported}");
                        Print("(copy the line above and paste in any console)");
                    })
                });

                apiType.GetMethod("RegisterMod")?.Invoke(null, new object[]
                {
                    "GlideRail",
                    "GlideRail",
                    "Blaster",
                    "Cinematic camera path recorder for CMS2026",
                    "https://github.com/iBl4St3R/GlideRail",
                    null,
                    null
                });

                Log.Msg("[GlideRail] Registered in SimpleConsole.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[GlideRail] Console registration failed: {ex.Message}");
            }
        }
    }
}