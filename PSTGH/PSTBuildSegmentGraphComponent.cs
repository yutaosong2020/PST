using Grasshopper.Kernel;
using PSTAlgo.Interop;
using System;

namespace PSTGH
{
    public class PSTBuildSegmentGraphComponent : GH_Component
    {
        public PSTBuildSegmentGraphComponent()
          : base("PST Build Segment Graph", "PSTBuildSeg",
                "Build a PST segment graph from prepared geometry.",
                "PSTGH", "Graph")
        { }

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("GraphInput", "G", "Prepared graph input.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddGenericParameter("SegmentGraph", "SG", "Native segment graph handle.", GH_ParamAccess.item);
            p.AddIntegerParameter("SegmentCount", "N", "Number of segments.", GH_ParamAccess.item);
            p.AddLineParameter("Segments", "L", "Segment lines in the exact order used by the engine (aligns with analysis outputs).", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            PSTGraphInputGoo goo = null;
            if (!DA.GetData(0, ref goo) || goo?.Value == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Missing GraphInput.");
                return;
            }
            var gi = goo.Value;
            if (gi.LineCoords == null || gi.LineCoords.Length < 4)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "GraphInput has no coordinates.");
                return;
            }

            // Build segment graph
            var desc = new CreateSegmentGraphDesc
            {
                Version = 1,
                LineCoords = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(gi.LineCoords, 0),
                Lines = gi.LineIndices != null && gi.LineIndices.Length > 0 ? System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(gi.LineIndices, 0) : IntPtr.Zero,
                LineCoordCount = (uint)(gi.LineIndices == null || gi.LineIndices.Length == 0 ? gi.LineCoords.Length / 2 : gi.LineCoords.Length / 2),
                LineCount = (uint)(gi.LineIndices == null || gi.LineIndices.Length == 0 ? gi.LineCoords.Length / 4 : gi.LineIndices.Length / 2),
                Progress = null,
                ProgressUser = IntPtr.Zero
            };
            var handle = NativeMethods.PSTACreateSegmentGraph(ref desc);
            if (handle == IntPtr.Zero)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "PSTACreateSegmentGraph failed.");
                return;
            }

            var segCount = (int)(gi.LineIndices == null || gi.LineIndices.Length == 0 ? gi.LineCoords.Length / 4 : gi.LineIndices.Length / 2);
            var sg = new PSTSegmentGraph(handle, gi.Hash, segCount);
            DA.SetData(0, new PSTSegmentGraphGoo(sg));
            DA.SetData(1, segCount);

            // Reconstruct the segment geometry in the same order the engine uses
            var lines = new System.Collections.Generic.List<Rhino.Geometry.Line>(segCount);
            if (gi.LineIndices == null || gi.LineIndices.Length == 0)
            {
                // coords are [x0,y0, x1,y1, ...] one segment per 4 doubles
                for (int i = 0; i < gi.LineCoords.Length; i += 4)
                {
                    var a = new Rhino.Geometry.Point3d(gi.LineCoords[i], gi.LineCoords[i + 1], 0);
                    var b = new Rhino.Geometry.Point3d(gi.LineCoords[i + 2], gi.LineCoords[i + 3], 0);
                    lines.Add(new Rhino.Geometry.Line(a, b));
                }
            }
            else
            {
                // coords are node positions [x,y, ...]; indices are [i0,j0, i1,j1, ...]
                for (int k = 0; k < gi.LineIndices.Length; k += 2)
                {
                    var ia = (int)gi.LineIndices[k];
                    var ib = (int)gi.LineIndices[k + 1];
                    var ax = gi.LineCoords[ia * 2];
                    var ay = gi.LineCoords[ia * 2 + 1];
                    var bx = gi.LineCoords[ib * 2];
                    var by = gi.LineCoords[ib * 2 + 1];
                    var a = new Rhino.Geometry.Point3d(ax, ay, 0);
                    var b = new Rhino.Geometry.Point3d(bx, by, 0);
                    lines.Add(new Rhino.Geometry.Line(a, b));
                }
            }
            DA.SetDataList(2, lines);
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("9A2C1B4B-2D93-47CA-9300-3F6B0F8C2B4A");
    }
}


