using Grasshopper.Kernel;
using PSTAlgo.Interop;
using System;

namespace PSTGH
{
    public class PSTAngularChoiceComponent : GH_Component
    {
        public PSTAngularChoiceComponent()
          : base("PST Angular Choice", "PSTAC",
                 "Compute Angular Choice on a segment graph (defaults hard-coded).",
                 "PSTGH", "Analysis")
        { }

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("SegmentGraph", "SG", "Segment graph handle.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddNumberParameter("Scores", "S", "Normalized choice scores.", GH_ParamAccess.list);
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

            var choice = new float[n];
            var nodeCounts = new uint[n];
            var totalDepths = new float[n];
            var totalDepthWeights = new float[n];

            var desc = new AngularChoiceDesc
            {
                Version = 2,
                SegmentGraph = sg.Handle,
                Radius = new Radii { Mask = 8, Angular = 10.0f },
                WeighByLength = false,
                AngleThreshold = 0.0f,
                AnglePrecision = 1,
                Progress = null,
                ProgressUser = IntPtr.Zero,
                OutChoice = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(choice, 0),
                OutNodeCount = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(nodeCounts, 0),
                OutTotalDepth = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(totalDepths, 0),
                OutTotalDepthWeight = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(totalDepthWeights, 0)
            };

            var ok = NativeMethods.PSTAAngularChoice(ref desc);
            if (!ok)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "PSTAAngularChoice failed.");
                return;
            }

            var scores = new float[n];
            NativeMethods.PSTAAngularChoiceNormalize(
                System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(choice, 0),
                System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(nodeCounts, 0),
                (uint)n,
                System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(scores, 0));

            DA.SetDataList(0, Array.ConvertAll(scores, x => (double)x));
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("2C3F2BDF-9448-4A4E-8996-FCF3FAD4387D");
    }
}



