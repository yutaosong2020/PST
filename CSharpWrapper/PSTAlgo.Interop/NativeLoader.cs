using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace PSTAlgo.Interop
{
    /// <summary>
    /// Ensures the native DLL (pstalgo64.dll) is found by the loader by pre-loading it from a known directory.
    /// Call Initialize once early in the plug-in startup with the plug-in folder path.
    /// </summary>
    public static class NativeLoader
    {
        private static bool s_initialized;

        public static void Initialize(string pluginDirectory)
        {
            if (s_initialized) return;
            if (string.IsNullOrWhiteSpace(pluginDirectory)) throw new ArgumentException("Plugin directory is required", nameof(pluginDirectory));

            var dllPath = Path.Combine(pluginDirectory, "pstalgo64.dll");
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException("Native library not found", dllPath);
            }

            // Attempt to load dependency directory first so dependent DLLs can resolve.
            // On older Windows versions SetDllDirectory affects DLL search path for the process.
            SetDllDirectory(pluginDirectory);

            var handle = LoadLibrary(dllPath);
            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to load native library: " + dllPath);
            }

            s_initialized = true;
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}


