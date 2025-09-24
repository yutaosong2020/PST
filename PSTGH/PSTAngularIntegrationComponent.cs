using Grasshopper.Kernel;
using PSTAlgo.Interop;
using System;

namespace PSTGH
{
    public class PSTAngularIntegrationComponent : GH_Component
    {
        public PSTAngularIntegrationComponent()
          : base("PST Angular Integration", "PSTAI",
                 "Compute Angular Integration on a segment graph (defaults hard-coded).",
                 "PSTGH", "Analysis")
        { }

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("SegmentGraph", "SG", "Segment graph handle.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddNumberParameter("Scores", "S", "Normalized integration scores.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            PSTSegmentGraphGoo goo = null;
            if (!DA.GetData(0, ref goo) || goo?.Value == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Missing SegmentGraph.");
                return;
            }
            var sg = goo.Value;
            var n = sg.SegmentCount;
            if (n <= 0 || sg.Handle == IntPtr.Zero)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid SegmentGraph.");
                return;
            }

            var nodeCounts = new uint[n];
            var totalDepths = new float[n];
            var totalWeights = new float[n];
            var totalDepthWeights = new float[n];

            var desc = new AngularIntegrationDesc
            {
                Version = 2,
                SegmentGraph = sg.Handle,
                Radius = new Radii { Mask = 8, Angular = 10.0f },
                WeighByLength = false,
                AngleThreshold = 0.0f,
                AnglePrecision = 1,
                Progress = null,
                ProgressUser = IntPtr.Zero,
                OutNodeCounts = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(nodeCounts, 0),
                OutTotalDepths = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(totalDepths, 0),
                OutTotalWeights = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(totalWeights, 0),
                OutTotalDepthWeights = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(totalDepthWeights, 0)
            };

            var ok = NativeMethods.PSTAAngularIntegration(ref desc);
            if (!ok)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "PSTAAngularIntegration failed.");
                return;
            }

            var scores = new float[n];
            NativeMethods.PSTAAngularIntegrationNormalize(
                System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(nodeCounts, 0),
                System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(totalDepths, 0),
                (uint)n,
                System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(scores, 0));

            DA.SetDataList(0, Array.ConvertAll(scores, x => (double)x));
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("B41B9C22-BB16-4E6F-8F69-6BCE7E3E8CF9");
    }
}



