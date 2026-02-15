using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using NewRhinoGold.Core;

namespace NewRhinoGold.Commands
{
    public sealed class MoveOnSurface : Command
    {
        public override string EnglishName => "MoveOnSurface";
        public override Guid Id => new Guid("E1F2A3B4-C5D6-4789-0123-456789ABCDEF");

        private Surface _lastSurface;
        private bool _copy;

        private const double DEFAULT_GAP = 0.2;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 1. Objekt(e) wählen
            var go = new GetObject { SubObjectSelect = false };
            go.SetCommandPrompt("Wähle Objekte zum Verschieben");
            go.EnablePreSelect(true, true);
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            var pickedObjects = new List<RhinoObject>();
            var bbox = BoundingBox.Empty;
            var previewItems = new List<MovePreviewItem>();

            foreach (var r in go.Objects())
            {
                var ro = r.Object();
                if (ro == null) continue;
                pickedObjects.Add(ro);
                bbox.Union(ro.Geometry.GetBoundingBox(true));

                GeometryBase mainGeo = ro.Geometry.Duplicate();
                Curve gapCurve = null;

                var gemData = ro.Attributes.UserData.Find(typeof(GemSmartData)) as GemSmartData;

                if (gemData != null && gemData.BaseCurve != null)
                {
                    var c = gemData.BaseCurve.DuplicateCurve();
                    double size = gemData.GemSize;
                    if (size > 0.001)
                    {
                        double scaleFactor = (size + (DEFAULT_GAP * 2)) / size;
                        var cBox = c.GetBoundingBox(true);
                        c.Transform(Transform.Scale(cBox.Center, scaleFactor));
                        gapCurve = c;
                    }
                }

                previewItems.Add(new MovePreviewItem
                {
                    Geometry = mainGeo,
                    GapCurve = gapCurve
                });
            }

            if (pickedObjects.Count == 0) return Result.Cancel;

            // 2. Copy-Option
            var copyToggle = new OptionToggle(_copy, "Nein", "Ja");
            var gopt = new GetOption();
            gopt.SetCommandPrompt("Kopieren?");
            gopt.AddOptionToggle("Copy", ref copyToggle);
            gopt.AcceptNothing(true);
            gopt.Get();
            if (gopt.Result() == GetResult.Option)
                _copy = copyToggle.CurrentValue;

            // 3. Fläche/Face wählen
            var gs = new GetObject { SubObjectSelect = true };
            gs.SetCommandPrompt(_lastSurface != null
                ? "Zielfläche wählen (Enter für letzte)"
                : "Zielfläche wählen");
            gs.GeometryFilter = ObjectType.Surface | ObjectType.Brep;
            gs.EnablePreSelect(false, true);
            gs.AcceptNothing(true);

            gs.SetCustomGeometryFilter((rhObj, geo, compIndex) =>
            {
                return geo is Surface
                    || compIndex.ComponentIndexType == ComponentIndexType.BrepFace;
            });

            var rs = gs.Get();
            Surface surface = null;

            if (rs == GetResult.Nothing)
            {
                if (_lastSurface == null) return Result.Cancel;
                surface = _lastSurface;
            }
            else if (rs == GetResult.Object)
            {
                var oref = gs.Object(0);
                surface = oref.Surface();
                if (surface == null)
                {
                    var face = oref.Face();
                    if (face != null) surface = face.UnderlyingSurface();
                }
                if (surface == null) return Result.Failure;
                _lastSurface = surface;
            }
            else
            {
                return Result.Cancel;
            }

            doc.Objects.UnselectAll();
            doc.Views.Redraw();

            // 4. Ankerpunkt berechnen
            var centerPoint = bbox.IsValid ? bbox.Center : Point3d.Origin;

            double u, v;
            if (!surface.ClosestPoint(centerPoint, out u, out v))
                return Result.Failure;

            Point3d anchorPoint = surface.PointAt(u, v);
            Vector3d sourceNormal = surface.NormalAt(u, v);
            if (!sourceNormal.Unitize())
                sourceNormal = Vector3d.ZAxis;

            Plane sourcePlane = new Plane(anchorPoint, sourceNormal);

            // 5. Getter starten
            var picker = new ObjectOnSurfaceGetter(
                surface, sourcePlane, previewItems);

            picker.SetCommandPrompt("Neuen Punkt auf Fläche wählen");
            picker.AcceptNothing(true);

            var getResult = picker.Get();

            if (getResult != GetResult.Point)
            {
                doc.Views.Redraw();
                return Result.Cancel;
            }

            // 6. Finale Transformation holen
            var xf = picker.FinalTransform;

            if (!xf.IsValid || xf == Transform.Identity)
            {
                RhinoApp.WriteLine("Fehler: Keine gültige Verschiebung.");
                return Result.Failure;
            }

            // 7. Anwenden
            var resultIds = new List<Guid>();
            doc.Objects.UnselectAll();

            uint sn = doc.BeginUndoRecord("Move on Surface");
            try
            {
                foreach (var ro in pickedObjects)
                {
                    var geom = ro.Geometry.Duplicate();
                    if (!geom.Transform(xf)) continue;

                    var attrs = ro.Attributes.Duplicate();
                    var newId = doc.Objects.Add(geom, attrs);

                    if (newId != Guid.Empty)
                    {
                        resultIds.Add(newId);
                        if (!_copy) doc.Objects.Delete(ro, true);
                    }
                }
            }
            finally
            {
                doc.EndUndoRecord(sn);
            }

            if (resultIds.Count > 0)
            {
                foreach (var id in resultIds) doc.Objects.Select(id);
                doc.Views.Redraw();
                RhinoApp.RunScript("_Gumball Toggle=On", false);
            }

            return Result.Success;
        }

        // ---------------------------------------------------------------
        private class MovePreviewItem
        {
            public GeometryBase Geometry;
            public Curve GapCurve;
        }

        // ---------------------------------------------------------------
        private class ObjectOnSurfaceGetter : GetPoint
        {
            private readonly Surface _surface;
            private readonly Plane _sourcePlane;
            private readonly List<MovePreviewItem> _items;

            // Vorschaufarben — gut sichtbar in allen Display-Modi
            private static readonly System.Drawing.Color PreviewColor =
                System.Drawing.Color.FromArgb(255, 255, 140, 0);   // Orange
            private static readonly System.Drawing.Color GapColor =
                System.Drawing.Color.FromArgb(255, 0, 255, 255);   // Cyan
            private static readonly System.Drawing.Color PointColor =
                System.Drawing.Color.FromArgb(255, 255, 255, 0);   // Gelb
            private const int PreviewThickness = 2;
            private const int GapThickness = 2;

            private Transform _xform = Transform.Identity;

            public Transform FinalTransform => _xform;

            public ObjectOnSurfaceGetter(
                Surface surface,
                Plane sourcePlane,
                List<MovePreviewItem> items)
            {
                _surface = surface;
                _sourcePlane = sourcePlane;
                _items = items;

                Constrain(surface, false);
                SetBasePoint(sourcePlane.Origin, true);
            }

            protected override void OnMouseMove(GetPointMouseEventArgs e)
            {
                base.OnMouseMove(e);
                ComputeTransform(e.Point);
            }

            private void ComputeTransform(Point3d pt)
            {
                double u, v;
                if (!_surface.ClosestPoint(pt, out u, out v))
                    return;

                Point3d sp = _surface.PointAt(u, v);
                Vector3d n = _surface.NormalAt(u, v);
                if (!n.Unitize()) return;

                Plane targetPlane = new Plane(sp, n);
                _xform = Transform.PlaneToPlane(_sourcePlane, targetPlane);
            }

            protected override void OnDynamicDraw(GetPointDrawEventArgs e)
            {
                ComputeTransform(e.CurrentPoint);

                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (item.Geometry == null) continue;

                    // Transformierte Kopie erstellen
                    var geo = item.Geometry.Duplicate();
                    geo.Transform(_xform);

                    // Zeichnen in Orange (Dicke 2) — sichtbar auf jeder Oberfläche
                    if (geo is Brep brep)
                    {
                        e.Display.DrawBrepWires(brep, PreviewColor, PreviewThickness);
                    }
                    else if (geo is Extrusion ext)
                    {
                        var extBrep = ext.ToBrep();
                        if (extBrep != null)
                            e.Display.DrawBrepWires(extBrep, PreviewColor, PreviewThickness);
                    }
                    else if (geo is Mesh mesh)
                    {
                        e.Display.DrawMeshWires(mesh, PreviewColor, PreviewThickness);
                    }
                    else if (geo is Curve crv)
                    {
                        e.Display.DrawCurve(crv, PreviewColor, PreviewThickness);
                    }

                    // Gap-Kurve in Cyan
                    if (item.GapCurve != null)
                    {
                        var gap = item.GapCurve.DuplicateCurve();
                        gap.Transform(_xform);
                        e.Display.DrawCurve(gap, GapColor, GapThickness);
                    }
                }

                // Zielpunkt
                e.Display.DrawPoint(e.CurrentPoint, PointColor);
            }
        }
    }
}