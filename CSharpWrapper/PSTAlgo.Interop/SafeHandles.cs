using Microsoft.Win32.SafeHandles;
using System;

namespace PSTAlgo.Interop
{
    public sealed class GraphHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public GraphHandle() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                NativeMethods.PSTAFreeGraph(handle);
            }
            return true;
        }
    }

    public sealed class SegmentGraphHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SegmentGraphHandle() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                NativeMethods.PSTAFreeSegmentGraph(handle);
            }
            return true;
        }
    }

    public sealed class SegmentGroupGraphHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SegmentGroupGraphHandle() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                NativeMethods.PSTAFreeSegmentGroupGraph(handle);
            }
            return true;
        }
    }
}


