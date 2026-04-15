using System;
using System.Runtime.InteropServices;

namespace GlideRail
{
    public static class GlideFileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrFilter;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrFileTitle;
            public int nMaxFileTitle;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrInitialDir;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetSaveFileNameW(ref OPENFILENAME ofn);

        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_NOCHANGEDIR = 0x00000008;
        private const int OFN_EXPLORER = 0x00080000;
        private const int OFN_OVERWRITEPROMPT = 0x00000002;

        private static string _lastDir = System.IO.Path.Combine(
            UnityEngine.Application.dataPath, "..", "Mods", "GlideRail", "Paths");

        /// <summary>Otwiera dialog wyboru pliku. Zwraca ścieżkę lub null.</summary>
        public static string OpenDialog(string title = "Load GlideRail Path")
        {
            return ShowDialog(title, false);
        }

        /// <summary>Otwiera dialog zapisu pliku. Zwraca ścieżkę lub null.</summary>
        public static string SaveDialog(string title = "Save GlideRail Path")
        {
            return ShowDialog(title, true);
        }

        private static string ShowDialog(string title, bool isSave)
        {
            const int BUF = 32767;
            IntPtr bufPtr = Marshal.AllocHGlobal((BUF + 1) * 2);
            try
            {
                for (int i = 0; i < (BUF + 1) * 2; i++)
                    Marshal.WriteByte(bufPtr, i, 0);

                // Upewnij się że katalog istnieje
                try { System.IO.Directory.CreateDirectory(_lastDir); } catch { }

                var ofn = new OPENFILENAME
                {
                    lStructSize = Marshal.SizeOf(typeof(OPENFILENAME)),
                    hwndOwner = IntPtr.Zero,
                    lpstrFilter = "GlideRail Path (*.json)\0*.json\0All Files\0*.*\0\0",
                    nFilterIndex = 1,
                    lpstrFile = bufPtr,
                    nMaxFile = BUF,
                    lpstrTitle = title,
                    lpstrInitialDir = _lastDir,
                    lpstrDefExt = "json",
                    Flags = isSave
                        ? OFN_NOCHANGEDIR | OFN_EXPLORER | OFN_OVERWRITEPROMPT
                        : OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST
                          | OFN_NOCHANGEDIR | OFN_EXPLORER
                };

                bool ok = isSave
                    ? GetSaveFileNameW(ref ofn)
                    : GetOpenFileNameW(ref ofn);

                if (!ok) return null;

                string path = Marshal.PtrToStringUni(bufPtr);
                if (!string.IsNullOrEmpty(path))
                    _lastDir = System.IO.Path.GetDirectoryName(path);

                return path;
            }
            finally
            {
                Marshal.FreeHGlobal(bufPtr);
            }
        }
    }
}