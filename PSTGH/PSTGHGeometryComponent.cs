using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSTGH
{
    public class PSTGHGeometryComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public PSTGHGeometryComponent()
          : base("PSTGHGeometryCleaning", "PSTGHGeoClean",
            "PSTGH Geometry Cleaning",
            "PSTGH", "Geometry")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Input lines/polylines.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Tolerance", "T", "Snap/Merge tolerance.", GH_ParamAccess.item, 0.001);
            pManager.AddBooleanParameter("Merge Collinear", "MC", "Merge collinear overlaps.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Build Indices", "BI", "Build node indices for topology.", GH_ParamAccess.item, true);
            pManager.AddTextParameter("Bake Layer", "Layer", "If not empty, bake cleaned segments to this layer with stable edge IDs in the Name attribute.", GH_ParamAccess.item, string.Empty);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("GraphInput", "G", "Prepared graph input.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Summary of cleaning.", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "R", "Detailed report.", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var curves = new List<Curve>();
            double tol = 0.001; bool mergeColl = false; bool buildIdx = true; string bakeLayer = string.Empty;
            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref tol);
            DA.GetData(2, ref mergeColl);
            DA.GetData(3, ref buildIdx);
            DA.GetData(4, ref bakeLayer);

            if (!_requestRun)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Click Run to clean.");
                // Reuse previous result if available
                if (_last != null)
                {
                    DA.SetData(0, new PSTGraphInputGoo(_last.Value.Input));
                    DA.SetData(1, _last.Value.Info);
                    DA.SetData(2, _last.Value.Report);
                }
                return;
            }

            var report = new StringBuilder();
            var cts = new CancellationTokenSource();
            var progress = 0.0;
            var prog = new Progress<double>(p => { progress = p; this.Message = $"Cleaning {p:0}%"; ExpirePreview(false); });

            var task = Task.Run(() => Clean(curves, tol, mergeColl, buildIdx, bakeLayer, (IProgress<double>)prog, cts.Token), cts.Token);
            task.Wait(cts.Token);
            var res = task.Result;

            DA.SetData(0, new PSTGraphInputGoo(res.Input));
            DA.SetData(1, res.Info);
            DA.SetData(2, res.Report);
            this.Message = "Done";
            _last = res;
            _requestRun = false;
        }

        private static (PSTGraphInput Input, string Info, string Report) Clean(IEnumerable<Curve> inCurves, double tol, bool mergeColl, bool buildIdx, string bakeLayer, IProgress<double> progress, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            progress?.Report(1);
            var allSegs = new List<Line>();
            int inputCount = 0, segCount = 0, zeroRemoved = 0, dupRemoved = 0, mergedNodes = 0, collinearMerges = 0;

            foreach (var c in inCurves)
            {
                ct.ThrowIfCancellationRequested();
                inputCount++;
                if (c == null) continue;
                if (c is LineCurve lc)
                {
                    var ln = lc.Line; if (ln.IsValid && ln.Length > tol) { allSegs.Add(ln); segCount++; }
                    else zeroRemoved++;
                }
                else if (c is PolylineCurve plc && plc.TryGetPolyline(out var pl))
                {
                    for (int i = 1; i < pl.Count; i++)
                    {
                        var ln = new Line(pl[i - 1], pl[i]);
                        if (ln.IsValid && ln.Length > tol) { allSegs.Add(ln); segCount++; } else zeroRemoved++;
                    }
                }
                else
                {
                    // Linearize NURBS or other curves using standard densification
                    var tpoly = c.ToPolyline(0,0,0,0,0,0,0,0,true);
                    if (tpoly != null && tpoly.IsValid)
                    {
                        var pl2 = tpoly.ToPolyline();
                        for (int i = 1; i < pl2.Count; i++)
                        {
                            var ln = new Line(pl2[i - 1], pl2[i]);
                            if (ln.IsValid && ln.Length > tol) { allSegs.Add(ln); segCount++; } else zeroRemoved++;
                        }
                    }
                }
            }

            progress?.Report(20);
            // Snap endpoints to tolerance (simple grid-based)
            var snapped = new Dictionary<(int,int), Point3d>();
            Point3d Snap(Point3d p)
            {
                var key = ((int)Math.Round(p.X / tol), (int)Math.Round(p.Y / tol));
                if (!snapped.TryGetValue(key, out var sp)) { sp = new Point3d(key.Item1 * tol, key.Item2 * tol, 0); snapped[key] = sp; }
                return sp;
            }
            for (int i = 0; i < allSegs.Count; i++)
            {
                var ln = allSegs[i];
                var a = Snap(ln.From); var b = Snap(ln.To);
                if (a.EpsilonEquals(b, tol)) { zeroRemoved++; allSegs[i] = new Line(); continue; }
                if (!a.EpsilonEquals(ln.From, tol)) mergedNodes++;
                if (!b.EpsilonEquals(ln.To, tol)) mergedNodes++;
                allSegs[i] = new Line(a, b);
            }
            allSegs = allSegs.Where(l => l.IsValid && l.Length > tol).ToList();

            progress?.Report(45);
            // Deduplicate (undirected)
            var set = new HashSet<(long,long)>() ;
            var uniq = new List<Line>();
            foreach (var ln in allSegs)
            {
                var a = ln.From; var b = ln.To;
                var ka = (((long)Math.Round(a.X / tol))<<20) ^ ((long)Math.Round(a.Y / tol));
                var kb = (((long)Math.Round(b.X / tol))<<20) ^ ((long)Math.Round(b.Y / tol));
                var key = ka <= kb ? (ka,kb) : (kb,ka);
                if (set.Add(key)) uniq.Add(ln); else dupRemoved++;
            }
            allSegs = uniq;

            progress?.Report(60);
            // Optional collinear merge (simple pass)
            if (mergeColl)
            {
                // Minimal implementation placeholder: skip heavy geometric merging for now
            }

            progress?.Report(70);
            // Build coords and optional indices
            double minX=double.MaxValue,minY=double.MaxValue,maxX=double.MinValue,maxY=double.MinValue;
            var coords = new List<double>(allSegs.Count*4);
            if (!buildIdx)
            {
                foreach (var ln in allSegs)
                {
                    minX=Math.Min(minX,Math.Min(ln.From.X,ln.To.X)); minY=Math.Min(minY,Math.Min(ln.From.Y,ln.To.Y));
                    maxX=Math.Max(maxX,Math.Max(ln.From.X,ln.To.X)); maxY=Math.Max(maxY,Math.Max(ln.From.Y,ln.To.Y));
                    coords.Add(ln.From.X); coords.Add(ln.From.Y); coords.Add(ln.To.X); coords.Add(ln.To.Y);
                }
                progress?.Report(85);
                var hash = HashCoords(coords);
                var input = new PSTGraphInput(coords.ToArray(), null, hash, new BoundingBox2D(minX,minY,maxX,maxY));
                var info = $"Input curves={inputCount}, Segs={segCount}, Zero/degenerate removed={zeroRemoved}, Duplicates removed={dupRemoved}, Nodes merged~={mergedNodes}, Final segs={allSegs.Count}";
                var rpt = $"Tolerance={tol}, BuildIndices={buildIdx}, MergeCollinear={mergeColl}\nBBox={input.BBox}";

                // Optional bake with stable IDs
                if (!string.IsNullOrWhiteSpace(bakeLayer))
                {
                    BakeSegments(allSegs, bakeLayer, tol);
                    info += ", Baked=Yes";
                }
                progress?.Report(100);
                sw.Stop();
                info += $", Time={sw.ElapsedMilliseconds}ms";
                return (input, info, rpt);
            }
            else
            {
                // Build node pool
                var nodeToIndex = new Dictionary<(int,int), uint>();
                var nodes = new List<Point3d>();
                var indices = new List<uint>(allSegs.Count*2);
                uint GetNodeIndex(Point3d p)
                {
                    var key = ((int)Math.Round(p.X / tol),(int)Math.Round(p.Y / tol));
                    if (!nodeToIndex.TryGetValue(key, out var idx)) { idx = (uint)nodes.Count; nodes.Add(new Point3d(key.Item1*tol,key.Item2*tol,0)); nodeToIndex[key]=idx; }
                    return idx;
                }
                foreach (var ln in allSegs)
                {
                    var ia = GetNodeIndex(ln.From); var ib = GetNodeIndex(ln.To);
                    indices.Add(ia); indices.Add(ib);
                    minX=Math.Min(minX,Math.Min(ln.From.X,ln.To.X)); minY=Math.Min(minY,Math.Min(ln.From.Y,ln.To.Y));
                    maxX=Math.Max(maxX,Math.Max(ln.From.X,ln.To.X)); maxY=Math.Max(maxY,Math.Max(ln.From.Y,ln.To.Y));
                }
                for (int i=0;i<nodes.Count;i++){ var p=nodes[i]; coords.Add(p.X); coords.Add(p.Y);} // node coords list
                progress?.Report(85);
                var hash = HashCoords(coords) + $"|E:{indices.Count/2}";
                var input = new PSTGraphInput(coords.ToArray(), indices.ToArray(), hash, new BoundingBox2D(minX,minY,maxX,maxY));
                var info = $"Input curves={inputCount}, Segs={segCount}, Zero removed={zeroRemoved}, Duplicates removed={dupRemoved}, Nodes merged~={mergedNodes}, Nodes={nodes.Count}, Final segs={indices.Count/2}";
                var rpt = $"Tolerance={tol}, BuildIndices={buildIdx}, MergeCollinear={mergeColl}\nBBox={input.BBox}";

                if (!string.IsNullOrWhiteSpace(bakeLayer))
                {
                    // Reconstruct baked lines from node coords + indices
                    var baked = new List<Line>(indices.Count/2);
                    for (int k=0;k<indices.Count;k+=2)
                    {
                        var ia = (int)indices[k];
                        var ib = (int)indices[k+1];
                        var a = nodes[ia]; var b = nodes[ib];
                        baked.Add(new Line(a,b));
                    }
                    BakeSegments(baked, bakeLayer, tol);
                    info += ", Baked=Yes";
                }
                progress?.Report(100);
                sw.Stop();
                info += $", Time={sw.ElapsedMilliseconds}ms";
                return (input, info, rpt);
            }
        }

        private static string HashCoords(List<double> coords)
        {
            using (var sha = SHA256.Create())
            {
                var arr = coords.ToArray();
                var bytes = new byte[arr.Length * sizeof(double)];
                Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private static void BakeSegments(IEnumerable<Line> segments, string layerPath, double tol)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc == null) return;

            int layerIndex = EnsureLayer(doc, layerPath);
            var attrs = new Rhino.DocObjects.ObjectAttributes { LayerIndex = layerIndex };
            foreach (var ln in segments)
            {
                var id = MakeEdgeId(ln.From, ln.To, tol);
                attrs.Name = id;
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

        private static string MakeEdgeId(Point3d a, Point3d b, double tol)
        {
            // Canonicalize order
            var aa = a; var bb = b;
            if (bb.X < aa.X || (Math.Abs(bb.X-aa.X) < 1e-12 && bb.Y < aa.Y)) { var t = aa; aa = bb; bb = t; }
            // Round to tolerance grid to stabilize
            double rx(double v) => Math.Round(v / tol) * tol;
            var s = $"{rx(aa.X):F6},{rx(aa.Y):F6}|{rx(bb.X):F6},{rx(bb.Y):F6}";
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                var h = sha.ComputeHash(bytes);
                // 8 hex chars
                return $"E_{BitConverter.ToString(h,0,4).Replace("-", string.Empty)}";
            }
        }

        // Button control
        private bool _requestRun = false;
        private (PSTGraphInput Input, string Info, string Report)? _last = null;

        public override void CreateAttributes()
        {
            m_attributes = new ButtonAttributes(this, () => { _requestRun = true; ExpireSolution(true); });
        }

        private sealed class ButtonAttributes : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
        {
            private readonly Action _onClick;
            public ButtonAttributes(GH_Component owner, Action onClick) : base(owner) { _onClick = onClick; }
            protected override void Layout()
            {
                base.Layout(); var r = Bounds; r.Height += 24; Bounds = r;
            }
            protected override void Render(Grasshopper.GUI.Canvas.GH_Canvas canvas, Grasshopper.GUI.Canvas.GH_CanvasChannel channel)
            {
                base.Render(canvas, channel);
                if (channel != Grasshopper.GUI.Canvas.GH_CanvasChannel.Objects) return;
                var button = new System.Drawing.RectangleF(Bounds.X + 5, Bounds.Bottom - 22, Bounds.Width - 10, 18);
                var g = canvas.Graphics;
                using (var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 80, 160, 80))) g.FillRectangle(b, button);
                using (var p = new System.Drawing.Pen(System.Drawing.Color.DarkGreen)) g.DrawRectangle(p, System.Drawing.Rectangle.Round(button));
                var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
                g.DrawString("Run", Grasshopper.GUI.GH_FontServer.Standard, System.Drawing.Brushes.Black, button, sf);
            }
            public override Grasshopper.GUI.Canvas.GH_ObjectResponse RespondToMouseDown(Grasshopper.GUI.Canvas.GH_Canvas sender, Grasshopper.GUI.GH_CanvasMouseEvent e)
            {
                var button = new System.Drawing.RectangleF(Bounds.X + 5, Bounds.Bottom - 22, Bounds.Width - 10, 18);
                if (e.Button == System.Windows.Forms.MouseButtons.Left && button.Contains(e.CanvasLocation))
                { _onClick(); return Grasshopper.GUI.Canvas.GH_ObjectResponse.Handled; }
                return base.RespondToMouseDown(sender, e);
            }
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("4473AF0D-8D96-483F-BE29-6A47CFDA4C02");
    }
}