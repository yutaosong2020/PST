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

                // Register logging
                int logHandle = 0;
                NativeMethods.LogCallback logCb = (level, domain, msg, user) =>
                {
                    var d = domain == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(domain);
                    var m = msg == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(msg);
                    Console.WriteLine($"LOG[{level}] {(d ?? "")} {m}");
                };
                try { logHandle = NativeMethods.PSTARegisterLogCallback(logCb, IntPtr.Zero); } catch { }

                Console.WriteLine("Building 10x10 grid...");
                var (lineCoords, lineCount) = BuildGridSegments(10, 10);
                var (gridCoords, gridIndices, gridLineCount) = BuildGridSegmentsWithIndices(10, 10);
                // Define origin points used by Reach and AttractionReach (for POINTS origin type)
                var originPts = new double[] { 2,2, 5,5, 8,8 };

                Console.WriteLine($"Creating segment graph with {lineCount} segments...");
                var segmentGraph = CreateSegmentGraph(lineCoords, lineCount);
                if (segmentGraph == IntPtr.Zero)
                    throw new InvalidOperationException("PSTACreateSegmentGraph returned null.");

                try
                {
                    // Reach test using an axial graph (needs PSTACreateGraph). Build simple axial lines from same coords.
                    Console.WriteLine("Creating axial graph (with origin points) for Reach/Attraction tests...");
                    var axialGraph = CreateAxialGraph(lineCoords, lineCount, originPts);
                    if (axialGraph == IntPtr.Zero)
                        throw new InvalidOperationException("PSTACreateGraph returned null.");
                    uint axialLineCount = 0;
                    uint axialPointCount = 0;
                    try
                    {
                        // Query axial graph info to determine expected output sizes per origin type
                        var ginfo = new GraphInfo { Version = 1 };
                        if (!NativeMethods.PSTAGetGraphInfo(axialGraph, ref ginfo)) throw new InvalidOperationException("PSTAGetGraphInfo failed");
                        Console.WriteLine($"Axial graph info: Lines={ginfo.LineCount}, Crossings={ginfo.CrossingCount}, Points={ginfo.PointCount}, Groups={ginfo.PointGroupCount}");
                        axialLineCount = ginfo.LineCount;
                        axialPointCount = ginfo.PointCount;

                        var originCounts = (uint)(originPts.Length / 2); // one origin per point now
                        var outReachedCount = new uint[originPts.Length / 2];
                        var outReachedLength = new float[originPts.Length / 2];
                        var outReachedArea = new float[originPts.Length / 2];
                        using (var pinOutCount = new Pinner(outReachedCount))
                        using (var pinOutLen = new Pinner(outReachedLength))
                        using (var pinOutArea = new Pinner(outReachedArea))
                        using (var pinOriginCoords = new Pinner(originPts))
                        {
                            var reachDesc = new ReachDesc
                            {
                                Version = 1,
                                Graph = axialGraph,
                                Radius = new Radii { Mask = 8, Angular = 10.0f },
                                OriginCoords = pinOriginCoords.Addr, // explicit origin points
                                OriginCount = originCounts,
                                Progress = null,
                                ProgressUser = IntPtr.Zero,
                                OutReachedCount = pinOutCount.Addr,
                                OutReachedLength = pinOutLen.Addr,
                                OutReachedArea = pinOutArea.Addr
                            };
                            Console.WriteLine("Running Reach...");
                            var okReach = NativeMethods.PSTAReach(ref reachDesc);
                            if (!okReach) Console.WriteLine("Reach failed (may require specific inputs)");
                            else
                            {
                                Console.WriteLine("First 5 reach counts:");
                                for (int i = 0; i < Math.Min(5, outReachedCount.Length); i++)
                                    Console.WriteLine($"[{i}] {outReachedCount[i]}");
                            }
                        }
                    }
                    finally
                    {
                        NativeMethods.PSTAFreeGraph(axialGraph);
                    }

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

                        // Segment Betweenness test (steps distance)
                        var betw = new float[lineCount];
                        using (var pinBetw = new Pinner(betw))
                        using (var pinN2 = new Pinner(nodeCounts))
                        using (var pinTD2 = new Pinner(totalDepths))
                        {
                            var betwDesc = new SegmentBetweennessDesc
                            {
                                Version = 1,
                                Graph = axialGraph, // betweenness uses HPSTAGraph
                                DistanceType = 2, // EPSTADistanceType_Steps
                                Radius = new Radii { Mask = 4, Steps = 10 },
                                Weights = IntPtr.Zero,
                                AttractionPoints = IntPtr.Zero,
                                AttractionPointCount = 0,
                                Progress = null,
                                ProgressUser = IntPtr.Zero,
                                OutBetweenness = pinBetw.Addr,
                                OutNodeCount = pinN2.Addr,
                                OutTotalDepth = pinTD2.Addr
                            };
                            Console.WriteLine("Running Segment Betweenness...");
                            var okBetw = NativeMethods.PSTASegmentBetweenness(ref betwDesc);
                            if (okBetw)
                            {
                                var betwNorm = new float[lineCount];
                                using (var pinBetwNorm = new Pinner(betwNorm))
                                {
                                    NativeMethods.PSTABetweennessNormalize(pinBetw.Addr, pinN2.Addr, count, pinBetwNorm.Addr);
                                    Console.WriteLine("First 5 normalized betweenness:");
                                    for (int i = 0; i < Math.Min(5, betwNorm.Length); i++)
                                        Console.WriteLine($"[{i}] {betwNorm[i]:F6}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Segment Betweenness failed (may require specific radius/graph)");
                            }
                        }

                        // Attraction Reach test using ORIGIN POINTS from the axial graph; one score per origin point
                        var attrScores = new float[axialPointCount];
                        using (var pinAttrScores = new Pinner(attrScores))
                        {
                            // Simple attraction points/values at grid corners and center
                            var attractionPts = new double[]
                            {
                                0,0,
                                9,9,
                                0,9,
                                9,0,
                                5,5
                            };
                            var attractionVals = new float[] { 1, 2, 1.5f, 1.5f, 3 };
                            using (var pinAttrPts = new Pinner(attractionPts))
                            using (var pinAttrVals = new Pinner(attractionVals))
                            {
                             var attrDesc = new AttractionReachDesc
                            {
                                Version = 1,
                                Graph = axialGraph,
                                OriginType = 0, // POINTS
                                 DistanceType = 6, // UNDEFINED (matches python tests' first pass)
                                 Radius = new Radii { Mask = 0 },
                                 WeightFunc = 0, // Constant
                                 WeightFuncConstant = 0.0f,
                                ScoreAccumulationMode = 0,
                                AttractionPoints = pinAttrPts.Addr,
                                AttractionPointCount = (uint)(attractionPts.Length / 2),
                                PointsPerAttractionPolygon = IntPtr.Zero,
                                AttractionPolygonCount = 0,
                                AttractionPolygonPointInterval = 0,
                                AttractionValues = pinAttrVals.Addr,
                                AttractionDistributionFunc = 1, // Divide
                                AttractionCollectionFunc = 0, // Average
                                Progress = null,
                                ProgressUser = IntPtr.Zero,
                                OutScores = pinAttrScores.Addr,
                                 OutputCount = axialPointCount
                            };
                            Console.WriteLine("Running Attraction Reach...");
                            var okAttr = NativeMethods.PSTAAttractionReach(ref attrDesc);
                            if (okAttr)
                            {
                                Console.WriteLine("First 5 attraction reach scores:");
                                for (int i = 0; i < Math.Min(5, attrScores.Length); i++)
                                    Console.WriteLine($"[{i}] {attrScores[i]:F6}");
                            }
                            else
                            {
                                Console.WriteLine("Attraction Reach failed (requires attraction inputs for meaningful scores)");
                            }
                            }
                        }
                    }
                }
                finally
                {
                    NativeMethods.PSTAFreeSegmentGraph(segmentGraph);
                }
                
                // Additional: Attraction Reach on a simple chain graph (mirrors Python test)
                Console.WriteLine("\nBuilding chain graph for Attraction Reach test (Python parity)...");
                var chainGraph = CreatePointOriginChainGraph(count: 5, length: 3.0, out var chainPointCount);
                try
                {
                    var chainAttrPts = new double[] { -1, 0, (5*3.0)+1, 0 };
                    var chainAttrVals = new float[] { 4, 3 };
                    var chainScores = new float[chainPointCount];
                    using (var pinChainAttrPts = new Pinner(chainAttrPts))
                    using (var pinChainAttrVals = new Pinner(chainAttrVals))
                    using (var pinChainScores = new Pinner(chainScores))
                    {
                        var desc = new AttractionReachDesc
                        {
                            Version = 1,
                            Graph = chainGraph,
                            OriginType = 0, // POINTS
                            DistanceType = 6, // UNDEFINED
                            Radius = new Radii { Mask = 0 },
                            WeightFunc = 0, // Constant
                            WeightFuncConstant = 0,
                            ScoreAccumulationMode = 0,
                            AttractionPoints = pinChainAttrPts.Addr,
                            AttractionPointCount = 2,
                            PointsPerAttractionPolygon = IntPtr.Zero,
                            AttractionPolygonCount = 0,
                            AttractionPolygonPointInterval = 0,
                            AttractionValues = pinChainAttrVals.Addr,
                            AttractionDistributionFunc = 1, // Divide
                            AttractionCollectionFunc = 0, // Average
                            Progress = null,
                            ProgressUser = IntPtr.Zero,
                            OutScores = pinChainScores.Addr,
                            OutputCount = chainPointCount
                        };
                        Console.WriteLine("Running Attraction Reach on chain graph...");
                        var ok = NativeMethods.PSTAAttractionReach(ref desc);
                        if (ok)
                        {
                            Console.WriteLine("Chain graph scores (first 5):");
                            for (int i = 0; i < Math.Min(5, chainScores.Length); i++)
                                Console.WriteLine($"[{i}] {chainScores[i]:F6}");
                        }
                        else
                        {
                            Console.WriteLine("Attraction Reach on chain graph failed");
                        }
                    }
                }
                finally
                {
                    NativeMethods.PSTAFreeGraph(chainGraph);
                }

                // Additional: Attraction Reach on grid axial graph using LINES as origins
                Console.WriteLine("\nCreating axial graph (lines origins) for Attraction Reach on grid...");
                var axialLinesGraph = CreateAxialGraphWithIndices(gridCoords, gridIndices, gridLineCount, originPoints: Array.Empty<double>());
                try
                {
                    var info = new GraphInfo { Version = 1 };
                    if (!NativeMethods.PSTAGetGraphInfo(axialLinesGraph, ref info)) throw new InvalidOperationException("PSTAGetGraphInfo failed (lines)");
                    var scoresLines = new float[info.LineCount];
                    var attrPts = new double[] { 0,0, 9,9, 0,9, 9,0, 5,5 };
                    var attrVals = new float[] { 1,2,1.5f,1.5f,3 };
                    using (var pinAttrPts = new Pinner(attrPts))
                    using (var pinAttrVals = new Pinner(attrVals))
                    using (var pinScores = new Pinner(scoresLines))
                    {
                        var desc = new AttractionReachDesc
                        {
                            Version = 1,
                            Graph = axialLinesGraph,
                            OriginType = 2, // LINES
                            DistanceType = 2, // STEPS
                            Radius = new Radii { Mask = 4, Steps = 2 },
                            WeightFunc = 0, // Constant
                            WeightFuncConstant = 0,
                            ScoreAccumulationMode = 0,
                            AttractionPoints = pinAttrPts.Addr,
                            AttractionPointCount = (uint)(attrPts.Length / 2),
                            PointsPerAttractionPolygon = IntPtr.Zero,
                            AttractionPolygonCount = 0,
                            AttractionPolygonPointInterval = 0,
                            AttractionValues = pinAttrVals.Addr,
                            AttractionDistributionFunc = 1,
                            AttractionCollectionFunc = 0,
                            Progress = null,
                            ProgressUser = IntPtr.Zero,
                            OutScores = pinScores.Addr,
                            OutputCount = info.LineCount
                        };
                        Console.WriteLine("Running Attraction Reach on grid (lines origins)...");
                        var ok = false;
                        try { ok = NativeMethods.PSTAAttractionReach(ref desc); }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"AttractionReach threw: {ex.Message}");
                        }
                        if (ok)
                        {
                            Console.WriteLine("Grid (lines) scores (first 5):");
                            for (int i = 0; i < Math.Min(5, (int)info.LineCount); i++)
                                Console.WriteLine($"[{i}] {scoresLines[i]:F6}");
                        }
                        else
                        {
                            Console.WriteLine("Attraction Reach on grid (lines origins) failed; trying UNDEFINED distance...");
                            // Fallback: UNDEFINED distance and empty radii
                            desc.DistanceType = 6; // UNDEFINED
                            desc.Radius = new Radii { Mask = 0 };
                            try { ok = NativeMethods.PSTAAttractionReach(ref desc); }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"AttractionReach (fallback) threw: {ex.Message}");
                            }
                            if (ok)
                            {
                                Console.WriteLine("Grid (lines, UNDEFINED) scores (first 5):");
                                for (int i = 0; i < Math.Min(5, (int)info.LineCount); i++)
                                    Console.WriteLine($"[{i}] {scoresLines[i]:F6}");
                            }
                            else
                            {
                                Console.WriteLine("Attraction Reach on grid (lines origins) still failed");
                            }
                        }
                    }
                }
                finally
                {
                    NativeMethods.PSTAFreeGraph(axialLinesGraph);
                }
                if (logHandle != 0) NativeMethods.PSTAUnregisterLogCallback(logHandle);
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

        private static IntPtr CreatePointOriginChainGraph(int count, double length, out uint pointCount)
        {
            // Build line coordinates for a straight chain
            var lineCoords = new List<double>();
            for (int x = 0; x <= count; x++) { lineCoords.Add(x * length); lineCoords.Add(0); }
            // Create point origins centered above each segment
            var points = new List<double>();
            for (int i = 0; i < count; i++) { points.Add((0.5 + i) * length); points.Add(1); }
            pointCount = (uint)points.Count / 2;

            using (var pinCoords = new Pinner(lineCoords.ToArray()))
            using (var pinPoints = new Pinner(points.ToArray()))
            {
                var desc = new CreateGraphDesc
                {
                    Version = 1,
                    LineCoords = pinCoords.Addr,
                    Lines = IntPtr.Zero,
                    LineCoordCount = (uint)(lineCoords.Count / 2),
                    LineCount = (uint)count,
                    UnlinkCoords = IntPtr.Zero,
                    UnlinkCount = 0,
                    PointCoords = pinPoints.Addr,
                    PointCount = (uint)(points.Count / 2),
                    PointsPerPolygon = IntPtr.Zero,
                    PolygonCount = 0,
                    PolygonPointInterval = 0,
                    Progress = null,
                    ProgressUser = IntPtr.Zero
                };
                return NativeMethods.PSTACreateGraph(ref desc);
            }
        }

        private static (double[] lineCoords, uint[] lineIndices, int lineCount) BuildGridSegmentsWithIndices(int cellsX, int cellsY)
        {
            var coords = new List<double>();
            var indices = new List<uint>();
            // Generate grid node coordinates, map to index
            int nodesX = cellsX + 1, nodesY = cellsY + 1;
            int NodeIndex(int x, int y) => y * nodesX + x;
            for (int y = 0; y < nodesY; y++)
                for (int x = 0; x < nodesX; x++) { coords.Add(x); coords.Add(y); }
            // Horizontal segments
            for (int y = 0; y < nodesY; y++)
                for (int x = 0; x < cellsX; x++) { indices.Add((uint)NodeIndex(x, y)); indices.Add((uint)NodeIndex(x + 1, y)); }
            // Vertical segments
            for (int x = 0; x < nodesX; x++)
                for (int y = 0; y < cellsY; y++) { indices.Add((uint)NodeIndex(x, y)); indices.Add((uint)NodeIndex(x, y + 1)); }
            int lineCount = indices.Count / 2;
            return (coords.ToArray(), indices.ToArray(), lineCount);
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

        private static IntPtr CreateAxialGraph(double[] lineCoords, int lineCount, double[] originPoints)
        {
            using (var pinCoords = new Pinner(lineCoords))
            using (var pinOriginPts = new Pinner(originPoints))
            {
                var desc = new CreateGraphDesc
                {
                    Version = 1,
                    LineCoords = pinCoords.Addr,
                    Lines = IntPtr.Zero,
                    LineCoordCount = (uint)(lineCoords.Length / 2),
                    LineCount = (uint)lineCount,
                    UnlinkCoords = IntPtr.Zero,
                    UnlinkCount = 0,
                    PointCoords = pinOriginPts.Addr,
                    PointCount = (uint)(originPoints.Length / 2),
                    PointsPerPolygon = IntPtr.Zero,
                    PolygonCount = 0,
                    PolygonPointInterval = 0,
                    Progress = null,
                    ProgressUser = IntPtr.Zero
                };
                return NativeMethods.PSTACreateGraph(ref desc);
            }
        }

        private static IntPtr CreateAxialGraphWithIndices(double[] lineCoords, uint[] lineIndices, int lineCount, double[] originPoints)
        {
            using (var pinCoords = new Pinner(lineCoords))
            using (var pinIndices = new Pinner(lineIndices))
            using (var pinOriginPts = new Pinner(originPoints))
            {
                var desc = new CreateGraphDesc
                {
                    Version = 1,
                    LineCoords = pinCoords.Addr,
                    Lines = pinIndices.Addr,
                    LineCoordCount = (uint)(lineCoords.Length / 2),
                    LineCount = (uint)lineCount,
                    UnlinkCoords = IntPtr.Zero,
                    UnlinkCount = 0,
                    PointCoords = originPoints.Length > 0 ? pinOriginPts.Addr : IntPtr.Zero,
                    PointCount = (uint)(originPoints.Length / 2),
                    PointsPerPolygon = IntPtr.Zero,
                    PolygonCount = 0,
                    PolygonPointInterval = 0,
                    Progress = null,
                    ProgressUser = IntPtr.Zero
                };
                return NativeMethods.PSTACreateGraph(ref desc);
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
