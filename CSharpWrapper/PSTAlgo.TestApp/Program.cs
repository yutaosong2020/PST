using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PSTAlgo.Interop;

namespace PSTAlgo.TestApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                NativeLoader.Initialize(baseDir);
                Smoke.ValidateStructs();

                Console.WriteLine("Building 10x10 grid...");
                var (lineCoords, lineCount) = BuildGridSegments(10, 10);

                Console.WriteLine($"Creating segment graph with {lineCount} segments...");
                var segmentGraph = CreateSegmentGraph(lineCoords, lineCount);
                if (segmentGraph == IntPtr.Zero)
                    throw new InvalidOperationException("PSTACreateSegmentGraph returned null.");

                try
                {
                    var count = (uint)lineCount;
                    var nodeCounts = new uint[lineCount];
                    var totalDepths = new float[lineCount];
                    var totalWeights = new float[lineCount];
                    var totalDepthWeights = new float[lineCount];

                    using (var pinNodeCounts = new Pinner(nodeCounts))
                    using (var pinTotalDepths = new Pinner(totalDepths))
                    using (var pinTotalWeights = new Pinner(totalWeights))
                    using (var pinTotalDepthWeights = new Pinner(totalDepthWeights))
                    {
                        var desc = new AngularIntegrationDesc
                        {
                            Version = 2,
                            SegmentGraph = segmentGraph,
                            Radius = new Radii
                            {
                                Mask = 8,
                                Angular = 10.0f
                            },
                            WeighByLength = false,
                            AngleThreshold = 0.0f,
                            AnglePrecision = 1,
                            Progress = null,
                            ProgressUser = IntPtr.Zero,
                            OutNodeCounts = pinNodeCounts.Addr,
                            OutTotalDepths = pinTotalDepths.Addr,
                            OutTotalWeights = pinTotalWeights.Addr,
                            OutTotalDepthWeights = pinTotalDepthWeights.Addr
                        };

                        Console.WriteLine("Running Angular Integration...");
                        var ok = NativeMethods.PSTAAngularIntegration(ref desc);
                        if (!ok) throw new InvalidOperationException("PSTAAngularIntegration failed.");

                        var scores = new float[lineCount];
                        using (var pinScores = new Pinner(scores))
                        {
                            NativeMethods.PSTAAngularIntegrationNormalize(
                                pinNodeCounts.Addr,
                                pinTotalDepths.Addr,
                                count,
                                pinScores.Addr);

                            Console.WriteLine("First 10 normalized scores:");
                            for (int i = 0; i < Math.Min(10, scores.Length); i++)
                            {
                                Console.WriteLine($"[{i}] {scores[i]:F6}");
                            }
                        }

                        // Angular Choice test
                        var choice = new float[lineCount];
                        using (var pinChoice = new Pinner(choice))
                        using (var pinN = new Pinner(nodeCounts))
                        using (var pinTD = new Pinner(totalDepths))
                        {
                            var choiceDesc = new AngularChoiceDesc
                            {
                                Version = 2,
                                SegmentGraph = segmentGraph,
                                Radius = new Radii { Mask = 8, Angular = 10.0f },
                                WeighByLength = false,
                                AngleThreshold = 0.0f,
                                AnglePrecision = 1,
                                Progress = null,
                                ProgressUser = IntPtr.Zero,
                                OutChoice = pinChoice.Addr,
                                OutNodeCount = pinNodeCounts.Addr,
                                OutTotalDepth = pinTotalDepths.Addr,
                                OutTotalDepthWeight = pinTotalDepthWeights.Addr
                            };

                            Console.WriteLine("Running Angular Choice...");
                            var okChoice = NativeMethods.PSTAAngularChoice(ref choiceDesc);
                            if (!okChoice) throw new InvalidOperationException("PSTAAngularChoice failed.");

                            var choiceNorm = new float[lineCount];
                            using (var pinChoiceNorm = new Pinner(choiceNorm))
                            {
                                NativeMethods.PSTAAngularChoiceNormalize(pinChoice.Addr, pinN.Addr, count, pinChoiceNorm.Addr);
                                Console.WriteLine("First 5 normalized choice:");
                                for (int i = 0; i < Math.Min(5, choiceNorm.Length); i++)
                                    Console.WriteLine($"[{i}] {choiceNorm[i]:F6}");
                            }
                        }
                    }
                }
                finally
                {
                    NativeMethods.PSTAFreeSegmentGraph(segmentGraph);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                Environment.ExitCode = -1;
            }
        }

        private static (double[] lineCoords, int lineCount) BuildGridSegments(int cellsX, int cellsY)
        {
            var segments = new List<double>( ((cellsY + 1) * cellsX + (cellsX + 1) * cellsY) * 4 );

            for (int y = 0; y <= cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    segments.Add(x); segments.Add(y);
                    segments.Add(x + 1); segments.Add(y);
                }
            }

            for (int x = 0; x <= cellsX; x++)
            {
                for (int y = 0; y < cellsY; y++)
                {
                    segments.Add(x); segments.Add(y);
                    segments.Add(x); segments.Add(y + 1);
                }
            }

            int lineCount = segments.Count / 4;
            return (segments.ToArray(), lineCount);
        }

        private static IntPtr CreateSegmentGraph(double[] lineCoords, int lineCount)
        {
            using (var pinCoords = new Pinner(lineCoords))
            {
            var desc = new CreateSegmentGraphDesc
            {
                Version = 1,
                LineCoords = pinCoords.Addr,
                Lines = IntPtr.Zero,
                LineCoordCount = (uint)(lineCoords.Length / 2),
                LineCount = (uint)lineCount,
                Progress = null,
                ProgressUser = IntPtr.Zero
            };
            return NativeMethods.PSTACreateSegmentGraph(ref desc);
            }
        }

        private sealed class Pinner : IDisposable
        {
            private GCHandle _handle;
            public IntPtr Addr { get; }
            public Pinner(Array array)
            {
                _handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                Addr = _handle.AddrOfPinnedObject();
            }
            public void Dispose()
            {
                if (_handle.IsAllocated) _handle.Free();
            }
        }
    }
}
