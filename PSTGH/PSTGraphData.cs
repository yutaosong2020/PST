using Grasshopper.Kernel.Types;
using PSTAlgo.Interop;
using System;

namespace PSTGH
{
    public sealed class PSTGraphInput
    {
        public double[] LineCoords { get; }
        public uint[] LineIndices { get; }
        public string Hash { get; }
        public BoundingBox2D BBox { get; }

        public PSTGraphInput(double[] coords, uint[] indices, string hash, BoundingBox2D bbox)
        {
            LineCoords = coords; LineIndices = indices; Hash = hash; BBox = bbox;
        }
    }

    public sealed class PSTSegmentGraph : IDisposable
    {
        public IntPtr Handle { get; private set; }
        public string SourceHash { get; }
        public int SegmentCount { get; }

        public PSTSegmentGraph(IntPtr handle, string hash, int segmentCount)
        {
            Handle = handle; SourceHash = hash; SegmentCount = segmentCount;
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                NativeMethods.PSTAFreeSegmentGraph(Handle);
                Handle = IntPtr.Zero;
            }
        }
    }

    public sealed class PSTGraphInputGoo : GH_Goo<PSTGraphInput>
    {
        public PSTGraphInputGoo() { }
        public PSTGraphInputGoo(PSTGraphInput value) : base(value) { }
        public override bool IsValid => Value != null && Value.LineCoords != null && Value.LineCoords.Length >= 4;
        public override string TypeName => "PSTGraphInput";
        public override string TypeDescription => "Prepared geometry for PST graph building.";
        public override IGH_Goo Duplicate() => new PSTGraphInputGoo(Value);
        public override string ToString() => Value == null ? "(null)" : $"PSTGraphInput Coords={Value.LineCoords.Length/2}pts Indices={(Value.LineIndices?.Length ?? 0)/2}lines Hash={Value.Hash}";
    }

    public sealed class PSTSegmentGraphGoo : GH_Goo<PSTSegmentGraph>
    {
        public PSTSegmentGraphGoo() { }
        public PSTSegmentGraphGoo(PSTSegmentGraph value) : base(value) { }
        public override bool IsValid => Value != null && Value.Handle != IntPtr.Zero;
        public override string TypeName => "PSTSegmentGraph";
        public override string TypeDescription => "Native segment graph handle.";
        public override IGH_Goo Duplicate() => new PSTSegmentGraphGoo(Value);
        public override string ToString() => Value == null ? "(null)" : $"PSTSegmentGraph Segments={Value.SegmentCount} Hash={Value.SourceHash}";
    }

    public readonly struct BoundingBox2D
    {
        public readonly double MinX, MinY, MaxX, MaxY;
        public BoundingBox2D(double minX, double minY, double maxX, double maxY)
        { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
        public override string ToString() => $"[{MinX:F3},{MinY:F3}] - [{MaxX:F3},{MaxY:F3}]";
    }
}


