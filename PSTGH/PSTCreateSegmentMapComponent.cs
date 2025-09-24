using Grasshopper.Kernel;
using Rhino.Geometry;
using PSTAlgo.Interop;
using System;
using System.Collections.Generic;

namespace PSTGH
{
    public class PSTCreateSegmentMapComponent : GH_Component
    {
        public PSTCreateSegmentMapComponent()
          : base("PST Create Segment Map", "PSTSegMap",
                 "Clean and segment polylines using the native PST engine.",
                 "PSTGH", "Geometry")
        { }

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddCurveParameter("Polylines", "P", "Input polylines/lines (will be packed and sent to the engine).", GH_ParamAccess.list);
            p.AddNumberParameter("Snap", "Snap", "Snap distance.", GH_ParamAccess.item, 1.0);
            p.AddNumberParameter("Tail", "Tail", "Minimum tail length to keep.", GH_ParamAccess.item, 10.0);
            p.AddNumberParameter("Deviation", "Dev", "Minimum 3-node colinear deviation.", GH_ParamAccess.item, 1.0);
            p.AddNumberParameter("ExtrudeCut", "Ext", "Extrude cut distance.", GH_ParamAccess.item, 0.0);
            p.AddIntegerParameter("RoadNetworkType", "RNT", "0=Axial/Segment, 1=RoadCentreLines.", GH_ParamAccess.item, 0);
            p.AddTextParameter("Bake Layer", "Layer", "If not empty, bake segments (Name=edgeId).", GH_ParamAccess.item, string.Empty);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddGenericParameter("GraphInput", "G", "Prepared graph input (nodes+indices).", GH_ParamAccess.item);
            p.AddTextParameter("Info", "I", "Summary.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var crvs = new List<Curve>();
            double snap=1, tail=10, dev=1, ext=0; int rnt=0; string bakeLayer="";
            if (!DA.GetDataList(0, crvs)) return;
            DA.GetData(1, ref snap); DA.GetData(2, ref tail); DA.GetData(3, ref dev); DA.GetData(4, ref ext); DA.GetData(5, ref rnt); DA.GetData(6, ref bakeLayer);

            // Pack polylines to engine layout: PolyCoords (x,y, ...), PolySections (int offsets), PolyCount
            var coords = new List<double>();
            var sections = new List<int>();
            int coordStart = 0; int polyCount = 0;
            foreach (var c in crvs)
            {
                if (c == null) continue;
                if (c is PolylineCurve plc && plc.TryGetPolyline(out var pl))
                {
                    sections.Add(coordStart);
                    for (int i=0;i<pl.Count;i++){ coords.Add(pl[i].X); coords.Add(pl[i].Y); }
                    coordStart = coords.Count/2; polyCount++;
                }
                else if (c is LineCurve lc)
                {
                    sections.Add(coordStart);
                    coords.Add(lc.PointAtStart.X); coords.Add(lc.PointAtStart.Y);
                    coords.Add(lc.PointAtEnd.X);   coords.Add(lc.PointAtEnd.Y);
                    coordStart = coords.Count/2; polyCount++;
                }
                else
                {
                    var tpoly = c.ToPolyline(0,0,0,0,0,0,0,0,true);
                    if (tpoly != null && tpoly.IsValid)
                    {
                        var pl2 = tpoly.ToPolyline();
                        sections.Add(coordStart);
                        for (int i=0;i<pl2.Count;i++){ coords.Add(pl2[i].X); coords.Add(pl2[i].Y); }
                        coordStart = coords.Count/2; polyCount++;
                    }
                }
            }
            sections.Add(coordStart); // end marker

            if (polyCount == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No valid polylines.");
                return;
            }

            var desc = new CreateSegmentMapDesc
            {
                Version = 2,
                Snap = (float)snap,
                ExtrudeCut = (float)ext,
                MinTail = (float)tail,
                Min3NodeColinearDeviation = (float)dev,
                RoadNetworkType = (byte)rnt,
                PolyCoords = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(coords.ToArray(), 0),
                PolySections = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(sections.ToArray(), 0),
                PolyCoordCount = (uint)(coords.Count/2),
                PolySectionCount = (uint)(sections.Count),
                PolyCount = (uint)polyCount,
                UnlinkCoords = IntPtr.Zero,
                UnlinkCount = 0,
                Progress = null,
                ProgressUser = IntPtr.Zero
            };

            var res = new CreateSegmentMapRes { Version = 2 };
            var algo = NativeMethods.PSTACreateSegmentMap(ref desc, ref res);
            if (algo == IntPtr.Zero)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "PSTACreateSegmentMap failed.");
                return;
            }

            // Read back node coords and segments
            var nodeCount = (int)desc.PolyCoordCount; // not necessarily equal; we have to trust res.SegmentCoords/Segments
            var segCount = (int)res.SegmentCount;
            var nodes = new double[res.SegmentCount * 0 + (int)(desc.PolyCoordCount*2)]; // we'll marshal from res.SegmentCoords with unknown count; instead, we reconstruct only segments and read coords per index

            // Marshal coords: we cannot infer count from res; assume they mirror input size or we can scan max index from Segments first
            var segTriples = new uint[segCount*3];
            System.Runtime.InteropServices.Marshal.Copy(res.Segments, (int[])(object)segTriples, 0, segTriples.Length);

            // Find max node index
            uint maxIdx = 0;
            for (int i=0;i<segTriples.Length;i+=3){ if (segTriples[i]>maxIdx) maxIdx=segTriples[i]; if (segTriples[i+1]>maxIdx) maxIdx=segTriples[i+1]; }
            var coordCount = (int)(maxIdx+1);
            var nodeCoords = new double[coordCount*2];
            System.Runtime.InteropServices.Marshal.Copy(res.SegmentCoords, nodeCoords, 0, nodeCoords.Length);

            // Build PSTGraphInput (nodes + indices)
            var gi = new PSTGraphInput(nodeCoords, ExtractIndices(segTriples), HashFrom(nodeCoords, segTriples), new BoundingBox2D(0,0,0,0));

            // Optional bake
            var info = $"Segments={segCount}, Nodes={coordCount}";
            if (!string.IsNullOrWhiteSpace(bakeLayer))
            {
                var lines = new List<Line>(segCount);
                for (int k=0;k<segTriples.Length;k+=3)
                {
                    int ia=(int)segTriples[k], ib=(int)segTriples[k+1];
                    var ax=nodeCoords[ia*2]; var ay=nodeCoords[ia*2+1];
                    var bx=nodeCoords[ib*2]; var by=nodeCoords[ib*2+1];
                    lines.Add(new Line(new Point3d(ax,ay,0), new Point3d(bx,by,0)));
                }
                BakeSegments(lines, bakeLayer, 1e-6);
                info += ", Baked=Yes";
            }

            DA.SetData(0, new PSTGraphInputGoo(gi));
            DA.SetData(1, info);
        }

        private static uint[] ExtractIndices(uint[] triples)
        {
            var idx = new uint[(triples.Length/3)*2];
            int j=0; for (int i=0;i<triples.Length;i+=3){ idx[j++]=triples[i]; idx[j++]=triples[i+1]; }
            return idx;
        }

        private static string HashFrom(double[] coords, uint[] indices)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = new byte[coords.Length*sizeof(double) + indices.Length*sizeof(uint)];
                Buffer.BlockCopy(coords,0,bytes,0,coords.Length*sizeof(double));
                Buffer.BlockCopy(indices,0,bytes,coords.Length*sizeof(double),indices.Length*sizeof(uint));
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("F1C3C33C-FAF1-4E8B-A6C1-0B5A3B9D5B55");

        // Reuse bake helper from geometry component
        private static void BakeSegments(IEnumerable<Line> segments, string layerPath, double tol)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc; if (doc == null) return;
            int layerIndex = EnsureLayer(doc, layerPath);
            var attrs = new Rhino.DocObjects.ObjectAttributes { LayerIndex = layerIndex };
            foreach (var ln in segments)
            {
                attrs.Name = $"{ln.From.X:F6},{ln.From.Y:F6}|{ln.To.X:F6},{ln.To.Y:F6}";
                doc.Objects.AddLine(ln, attrs);
            }
            doc.Views.Redraw();
        }
        private static int EnsureLayer(Rhino.RhinoDoc doc, string layerPath)
        {
            var name = layerPath.Trim();
            int idx = doc.Layers.FindByFullPath(name, -1);
            if (idx >= 0) return idx;
            var layer = new Rhino.DocObjects.Layer { Name = name };
            return doc.Layers.Add(layer);
        }
    }
}



