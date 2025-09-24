using System;
using System.Runtime.InteropServices;

namespace PSTAlgo.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Radii
    {
        public uint Mask;
        public float Straight;
        public float Walking;
        public uint Steps;
        public float Angular;
        public float Axmeter;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CreateGraphDesc
    {
        public uint Version;

        // Lines
        public IntPtr LineCoords;     // double*
        public IntPtr Lines;          // uint*
        public uint LineCoordCount;   // number of 2D coords (pairs)
        public uint LineCount;        // number of lines

        // Unlinks (optional)
        public IntPtr UnlinkCoords;   // double*
        public uint UnlinkCount;      // pairs

        // Points (optional)
        public IntPtr PointCoords;    // double*
        public uint PointCount;       // pairs

        // Polygons (optional)
        public IntPtr PointsPerPolygon; // uint*
        public uint PolygonCount;
        public float PolygonPointInterval;

        // Progress
        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphInfo
    {
        public uint Version;
        public uint LineCount;
        public uint CrossingCount;
        public uint PointCount;
        public uint PointGroupCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CreateSegmentGraphDesc
    {
        public uint Version;
        public IntPtr LineCoords;   // double*
        public IntPtr Lines;        // uint*
        public uint LineCoordCount; // pairs
        public uint LineCount;      // lines
        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CreateSegmentGroupGraphDesc
    {
        public uint Version;
        public IntPtr SegmentGraph; // void*
        public IntPtr GroupIndexPerSegment; // uint*
        public uint SegmentCount;
        public uint GroupCount;
        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AngularIntegrationDesc
    {
        public uint Version;
        public IntPtr SegmentGraph; // HPSTASegmentGraph
        public Radii Radius;

        [MarshalAs(UnmanagedType.I1)]
        public bool WeighByLength;
        public float AngleThreshold;
        public uint AnglePrecision;

        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;

        // Outputs (optional, can be NULL)
        public IntPtr OutNodeCounts;        // uint*
        public IntPtr OutTotalDepths;       // float*
        public IntPtr OutTotalWeights;      // float*
        public IntPtr OutTotalDepthWeights; // float*
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AngularChoiceDesc
    {
        public uint Version;              // 2
        public IntPtr SegmentGraph;       // HPSTASegmentGraph
        public Radii Radius;

        [MarshalAs(UnmanagedType.I1)]
        public bool WeighByLength;
        public float AngleThreshold;
        public uint AnglePrecision;

        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;

        public IntPtr OutChoice;            // float*
        public IntPtr OutNodeCount;         // uint*
        public IntPtr OutTotalDepth;        // float*
        public IntPtr OutTotalDepthWeight;  // float*
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ReachDesc
    {
        public uint Version;            // 1
        public IntPtr Graph;            // HPSTAGraph
        public Radii Radius;
        public IntPtr OriginCoords;     // double2* (x,y pairs)
        public uint OriginCount;
        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;
        public IntPtr OutReachedCount;   // uint*
        public IntPtr OutReachedLength;  // float*
        public IntPtr OutReachedArea;    // float*
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SegmentBetweennessDesc
    {
        public uint Version;           // 1
        public IntPtr Graph;           // HPSTAGraph
        public byte DistanceType;      // EPSTADistanceType
        public Radii Radius;
        public IntPtr Weights;         // float*
        public IntPtr AttractionPoints; // double*
        public uint AttractionPointCount;
        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;
        public IntPtr OutBetweenness;  // float*
        public IntPtr OutNodeCount;    // uint*
        public IntPtr OutTotalDepth;   // float*
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AttractionReachDesc
    {
        public uint Version;           // 1
        public IntPtr Graph;           // HPSTAGraph
        public byte OriginType;        // EOriginType
        public byte DistanceType;      // EPSTADistanceType
        public Radii Radius;
        public byte WeightFunc;        // EWeightFunc
        public float WeightFuncConstant;
        public byte ScoreAccumulationMode; // EScoreAccumulationMode (deprecated enum)
        public IntPtr AttractionPoints;   // double2*
        public uint AttractionPointCount;
        public IntPtr PointsPerAttractionPolygon; // uint*
        public uint AttractionPolygonCount;
        public float AttractionPolygonPointInterval;
        public IntPtr AttractionValues; // float*
        public byte AttractionDistributionFunc; // EAttractionDistributionFunc
        public byte AttractionCollectionFunc;   // EAttractionCollectionFunc
        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;
        public IntPtr OutScores; // float*
        public uint OutputCount; // verify
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CreateSegmentMapDesc
    {
        public uint Version; // 2
        public float Snap;
        public float ExtrudeCut;
        public float MinTail;
        public float Min3NodeColinearDeviation;
        public byte RoadNetworkType; // EPSTARoadNetworkType
        public IntPtr PolyCoords;     // double*
        public IntPtr PolySections;   // int*
        public uint PolyCoordCount;
        public uint PolySectionCount;
        public uint PolyCount;
        public IntPtr UnlinkCoords;   // double*
        public uint UnlinkCount;
        public NativeMethods.ProgressCallback Progress;
        public IntPtr ProgressUser;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CreateSegmentMapRes
    {
        public uint Version; // 2
        public IntPtr SegmentCoords;   // double*
        public IntPtr Segments;        // unsigned int* (P0, P1, Base)
        public uint SegmentCount;
        public IntPtr UnlinkCoords;    // double*
        public uint UnlinkCount;
    }
}


