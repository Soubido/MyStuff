using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace NewRhinoGold.Commands
{
    public sealed class MoveOnSurface : Command
    {
        public override string EnglishName => "MoveOnSurface";
        public override Guid Id => new Guid("E1F2A3B4-C5D6-4789-0123-456789ABCDEF");

        private Surface _lastSurface;
        private bool _copy; // true = kopieren, false = Original ersetzen

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
            foreach (var r in go.Objects())
            {
                var ro = r.Object();
                if (ro == null) continue;
                pickedObjects.Add(ro);
                bbox.Union(ro.Geometry.GetBoundingBox(true));
            }
            if (pickedObjects.Count == 0) return Result.Cancel;

            // 2. Copy-Option
            var copyToggle = new OptionToggle(_copy, "Nein", "Ja");
            var gopt = new GetOption();
            gopt.SetCommandPrompt("Kopieren?");
            int idxCopy = gopt.AddOptionToggle("Copy", ref copyToggle);
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

            // Filter: Nur Flächen oder Brep-Faces erlauben
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

            // 4. Punkt auf Fläche wählen (mit Live-Vorschau)
            var basePoint = bbox.IsValid ? bbox.Center : Point3d.Origin;

            var picker = new ObjectOnSurfaceGetter(surface, basePoint, pickedObjects);
            picker.SetCommandPrompt("Punkt auf Fläche wählen");
            picker.AcceptNothing(true);

            if (picker.Get() != GetResult.Point)
                return Result.Cancel;

            // 5. Transformation anwenden
            var xf = picker.CalculatedTransform;

            var resultIds = new List<Guid>();
            doc.Objects.UnselectAll();

            uint sn = doc.BeginUndoRecord("Move on Surface");
            try
            {
                foreach (var ro in pickedObjects)
                {
                    var geom = ro.Geometry.Duplicate();

                    // Transformieren (Triggered OnTransform in SmartObjects)
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

                // Gumball aktivieren
                RhinoApp.RunScript("_Gumball Toggle=On", false);
            }

            return Result.Success;
        }

        /// <summary>
        /// Interne Helper-Klasse für die Vorschau während des Verschiebens.
        /// </summary>
        private class ObjectOnSurfaceGetter : GetPoint
        {
            private readonly Surface _surface;
            private readonly Point3d _baseCenter;
            private readonly List<GeometryBase> _previewGeometries;
            public Transform CalculatedTransform { get; private set; }

            public ObjectOnSurfaceGetter(Surface surface, Point3d baseCenter, List<RhinoObject> objects)
            {
                _surface = surface;
                _baseCenter = baseCenter;
                _previewGeometries = new List<GeometryBase>();

                foreach (var obj in objects)
                {
                    if (obj.Geometry != null)
                        _previewGeometries.Add(obj.Geometry.Duplicate());
                }

                Constrain(_surface, false);
            }

            protected override void OnMouseMove(GetPointMouseEventArgs e)
            {
                base.OnMouseMove(e);

                // Berechne Translation vom Original-Zentrum zum aktuellen Mauspunkt
                var translation = e.Point - _baseCenter;
                CalculatedTransform = Transform.Translation(translation);
            }

            protected override void OnDynamicDraw(GetPointDrawEventArgs e)
            {
                if (CalculatedTransform.IsValid)
                {
                    foreach (var geo in _previewGeometries)
                    {
                        // Performance: ModelTransform pushen statt Geometrie zu transformieren
                        e.Display.PushModelTransform(CalculatedTransform);

                        if (geo is Brep b) e.Display.DrawBrepWires(b, System.Drawing.Color.Gray);
                        else if (geo is Mesh m) e.Display.DrawMeshWires(m, System.Drawing.Color.Gray);
                        else if (geo is Curve c) e.Display.DrawCurve(c, System.Drawing.Color.Gray);

                        e.Display.PopModelTransform();
                    }
                }
                base.OnDynamicDraw(e);
            }
        }
    }
}