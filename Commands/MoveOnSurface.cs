using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using NewRhinoGold.Core; // WICHTIG: Damit GemSmartData gefunden wird

namespace NewRhinoGold.Commands
{
    public sealed class MoveOnSurface : Command
    {
        public override string EnglishName => "MoveOnSurface";
        public override Guid Id => new Guid("E1F2A3B4-C5D6-4789-0123-456789ABCDEF");

        private Surface _lastSurface;
        private bool _copy;

        // Standard Gap in mm
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

            // NEU: Wir bereiten die Vorschau-Daten schon hier vor
            var previewItems = new List<MovePreviewItem>();

            foreach (var r in go.Objects())
            {
                var ro = r.Object();
                if (ro == null) continue;
                pickedObjects.Add(ro);
                bbox.Union(ro.Geometry.GetBoundingBox(true));

                // --- NEU: SMART DATA CHECK & GAP BERECHNUNG ---
                GeometryBase mainGeo = ro.Geometry.Duplicate();
                Curve gapCurve = null;

                // Versuchen, SmartData zu finden
                var gemData = ro.Attributes.UserData.Find(typeof(GemSmartData)) as GemSmartData;

                if (gemData != null && gemData.BaseCurve != null)
                {
                    // 1. Kurve holen (ist dank OnTransform schon an der richtigen Stelle)
                    var c = gemData.BaseCurve.DuplicateCurve();

                    // 2. Skalierung berechnen für 0.2mm Gap
                    // Formel: (Size + 2*Gap) / Size
                    double size = gemData.GemSize;
                    if (size > 0.001)
                    {
                        double scaleFactor = (size + (DEFAULT_GAP * 2)) / size;

                        // Skalieren um das Zentrum der Kurve
                        var cBox = c.GetBoundingBox(true);
                        var xformScale = Transform.Scale(cBox.Center, scaleFactor);
                        c.Transform(xformScale);

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
            gs.SetCommandPrompt(_lastSurface != null ? "Zielfläche wählen (Enter für letzte)" : "Zielfläche wählen");
            gs.GeometryFilter = ObjectType.Surface | ObjectType.Brep;
            gs.EnablePreSelect(false, true);
            gs.AcceptNothing(true);

            gs.SetCustomGeometryFilter((rhObj, geo, compIndex) =>
            {
                return geo is Surface || compIndex.ComponentIndexType == ComponentIndexType.BrepFace;
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

            // Deselektieren für saubere Ansicht
            doc.Objects.UnselectAll();
            doc.Views.Redraw();

            // 4. ANKERPUNKT BERECHNEN (Abstand halten Logik)
            var centerPoint = bbox.IsValid ? bbox.Center : Point3d.Origin;

            double u, v;
            if (!surface.ClosestPoint(centerPoint, out u, out v))
            {
                return Result.Failure;
            }
            Point3d anchorPoint = surface.PointAt(u, v);

            // 5. GETTER STARTEN (mit den neuen Preview Items)
            var picker = new ObjectOnSurfaceGetter(surface, anchorPoint, previewItems);
            picker.SetCommandPrompt("Neuen Punkt auf Fläche wählen");
            picker.AcceptNothing(true);

            if (picker.Get() != GetResult.Point)
                return Result.Cancel;

            // 6. Transformation anwenden
            var xf = picker.CalculatedTransform;

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

                    // WICHTIG: Wenn wir kopieren, müssen wir neue IDs für SmartData vergeben?
                    // SmartData ist UserData, das wird normalerweise einfach mitkopiert.

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

        // --- HILFSKLASSE FÜR VORSCHAU-DATEN ---
        private class MovePreviewItem
        {
            public GeometryBase Geometry;
            public Curve GapCurve; // Kann null sein, wenn kein Gem
        }

        // --- GETTER KLASSE ---
        private class ObjectOnSurfaceGetter : GetPoint
        {
            private readonly Surface _surface;
            private readonly Point3d _anchorPoint;
            private readonly List<MovePreviewItem> _items; // Geändert von GeometryBase zu MovePreviewItem

            public Transform CalculatedTransform { get; private set; }

            public ObjectOnSurfaceGetter(Surface surface, Point3d anchorPoint, List<MovePreviewItem> items)
            {
                _surface = surface;
                _anchorPoint = anchorPoint;
                _items = items;

                Constrain(_surface, false);
            }

            protected override void OnMouseMove(GetPointMouseEventArgs e)
            {
                base.OnMouseMove(e);
                var translation = e.Point - _anchorPoint;
                CalculatedTransform = Transform.Translation(translation);
            }

            protected override void OnDynamicDraw(GetPointDrawEventArgs e)
            {
                if (!CalculatedTransform.IsValid) return;

                // Einmal Transformation auf den Stack legen für alle Objekte
                e.Display.PushModelTransform(CalculatedTransform);

                foreach (var item in _items)
                {
                    // 1. Geometrie zeichnen (Grau)
                    if (item.Geometry is Brep b) e.Display.DrawBrepWires(b, System.Drawing.Color.Gray);
                    else if (item.Geometry is Mesh m) e.Display.DrawMeshWires(m, System.Drawing.Color.Gray);
                    else if (item.Geometry is Curve c) e.Display.DrawCurve(c, System.Drawing.Color.Gray);

                    // 2. Gap Kurve zeichnen (Cyan), falls vorhanden
                    if (item.GapCurve != null)
                    {
                        e.Display.DrawCurve(item.GapCurve, System.Drawing.Color.Cyan, 2);
                    }
                }

                e.Display.PopModelTransform();

                base.OnDynamicDraw(e);
            }
        }
    }
}