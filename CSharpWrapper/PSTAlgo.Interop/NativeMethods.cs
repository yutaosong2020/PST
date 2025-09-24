using System;
using System.Runtime.InteropServices;

namespace PSTAlgo.Interop
{
    public static class NativeMethods
    {
        private const string DllName = "pstalgo64.dll";

        // Delegates
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ProgressCallback(
            IntPtr text, // UTF-8/ANSI char*
            float progress,
            IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallback(
            int level,
            IntPtr domain, // UTF-8/ANSI char*
            IntPtr message, // UTF-8/ANSI char*
            IntPtr userData);

        // Core / Logging
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTARegisterLogCallback")]
        public static extern int PSTARegisterLogCallback(LogCallback callback, IntPtr userData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAUnregisterLogCallback")]
        public static extern void PSTAUnregisterLogCallback(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAStandardNormalize")]
        public static extern void PSTAStandardNormalize(IntPtr inScores, uint count, IntPtr outNormalized);

        // Graphs (Axial/Line Graph)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTACreateGraph")]
        public static extern IntPtr PSTACreateGraph(ref CreateGraphDesc desc);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAFreeGraph")]
        public static extern void PSTAFreeGraph(IntPtr graphHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAGetGraphInfo")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool PSTAGetGraphInfo(IntPtr graphHandle, out GraphInfo info);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAGetGraphLineLengths")]
        public static extern uint PSTAGetGraphLineLengths(IntPtr graphHandle, IntPtr outLengths, uint lengthCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAGetGraphCrossingCoords")]
        public static extern uint PSTAGetGraphCrossingCoords(IntPtr graphHandle, IntPtr outCoords, uint coordPairsCount);

        // Segment Graph
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTACreateSegmentGraph")]
        public static extern IntPtr PSTACreateSegmentGraph(ref CreateSegmentGraphDesc desc);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAFreeSegmentGraph")]
        public static extern void PSTAFreeSegmentGraph(IntPtr segmentGraphHandle);

        // Segment Group Graph
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTACreateSegmentGroupGraph")]
        public static extern IntPtr PSTACreateSegmentGroupGraph(ref CreateSegmentGroupGraphDesc desc);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAFreeSegmentGroupGraph")]
        public static extern void PSTAFreeSegmentGroupGraph(IntPtr handle);

        // Angular Integration
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularIntegration")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool PSTAAngularIntegration(ref AngularIntegrationDesc desc);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularIntegrationNormalize")]
        public static extern void PSTAAngularIntegrationNormalize(IntPtr nodeCounts, IntPtr totalDepth, uint count, IntPtr outScores);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularIntegrationNormalizeLengthWeight")]
        public static extern void PSTAAngularIntegrationNormalizeLengthWeight(IntPtr reachedLength, IntPtr totalDepth, uint count, IntPtr outScores);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularIntegrationSyntaxNormalize")]
        public static extern void PSTAAngularIntegrationSyntaxNormalize(IntPtr nodeCounts, IntPtr totalDepth, uint count, IntPtr outScores);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularIntegrationSyntaxNormalizeLengthWeight")]
        public static extern void PSTAAngularIntegrationSyntaxNormalizeLengthWeight(IntPtr reachedLength, IntPtr totalDepth, uint count, IntPtr outScores);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularIntegrationHillierNormalize")]
        public static extern void PSTAAngularIntegrationHillierNormalize(IntPtr nodeCounts, IntPtr totalDepth, uint count, IntPtr outScores);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularIntegrationHillierNormalizeLengthWeight")]
        public static extern void PSTAAngularIntegrationHillierNormalizeLengthWeight(IntPtr reachedLength, IntPtr totalDepth, uint count, IntPtr outScores);

        // Angular Choice
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularChoice")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool PSTAAngularChoice(ref AngularChoiceDesc desc);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularChoiceNormalize")]
        public static extern void PSTAAngularChoiceNormalize(IntPtr inScores, IntPtr N, uint count, IntPtr outScores);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAAngularChoiceSyntaxNormalize")]
        public static extern void PSTAAngularChoiceSyntaxNormalize(IntPtr inScores, IntPtr TD, uint count, IntPtr outScores);

        // Reach
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PSTAReach")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool PSTAReach(ref ReachDesc desc);
    }
}


