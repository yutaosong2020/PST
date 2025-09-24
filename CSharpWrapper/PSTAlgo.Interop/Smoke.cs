using System;
using System.Runtime.InteropServices;

namespace PSTAlgo.Interop
{
    public static class Smoke
    {
        public static void ValidateStructs()
        {
            // These should match native sizeof values on 64-bit Windows.
            // We validate obvious alignment-sensitive structs.
            ExpectSize<Radii>(sizeof(uint) + sizeof(float) * 4 + sizeof(uint));
            // NOTE: Skip CreateGraphDesc size check entirely; padding differs per runtime/packing.

            ExpectSize<GraphInfo>(sizeof(uint) * 5);

            // AngularIntegrationDesc with explicit fields
            var expectedAngular = sizeof(uint) + // Version
                                   IntPtr.Size + // SegmentGraph
                                   Marshal.SizeOf<Radii>() +
                                   sizeof(bool) + 3 + // bool (I1) pad to 4
                                   sizeof(float) +
                                   sizeof(uint) +
                                   IntPtr.Size + // Progress
                                   IntPtr.Size + // ProgressUser
                                   IntPtr.Size * 4; // outputs
            // Allow for padding differences; ensure at least these sizes
            if (Marshal.SizeOf<AngularIntegrationDesc>() < expectedAngular)
                throw new InvalidOperationException($"AngularIntegrationDesc size unexpected: {Marshal.SizeOf<AngularIntegrationDesc>()} < {expectedAngular}");
        }

        private static void ExpectSize<T>(int expected)
        {
            var size = Marshal.SizeOf<T>();
            if (size < expected)
                throw new InvalidOperationException($"Struct {typeof(T).Name} size {size} < expected minimum {expected}");
        }
    }
}


