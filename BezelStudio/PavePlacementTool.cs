using System;
using System.Collections.Generic;
using System.Drawing; // Wichtig für Color
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using NewRhinoGold.Core;

namespace NewRhinoGold.BezelStudio
{
    public class PavePlacementTool : GetPoint
    {
        // ... (Alle Felder bleiben gleich)
        private readonly Brep _targetBrep;
        private readonly double _sizeX;
        private readonly double _sizeY;
        private readonly double _gap;
        private readonly string _shapeName;
        private readonly bool _flip;
        private readonly bool _symX;
        private readonly bool _symY;
        private readonly bool _allowCollision;
        private readonly List<ExistingGemData> _existingGems;
        private readonly System.Drawing.Color _gemColor;

        // Hier nutzen wir jetzt die externe Klasse TempGem
        private List<TempGem> _previewGems = new List<TempGem>();

        // Konstruktor
        public PavePlacementTool(Brep target, double sizeX, double sizeY, double gap, string shape, bool flip, bool symX, bool symY, bool allowCollision, List<ExistingGemData> existing, System.Drawing.Color gemColor)
        {
            _targetBrep = target;
            _sizeX = sizeX;
            _sizeY = sizeY;
            _gap = gap;
            _shapeName = shape;
            _flip = flip;
            _symX = symX;
            _symY = symY;
            _allowCollision = allowCollision;
            _existingGems = existing ?? new List<ExistingGemData>();
            _gemColor = gemColor;

            Constrain(_targetBrep, -1, -1, false);
        }

        // --- HIER IST DIE LISTE, DIE SIE WOLLTEN ---
        // Diese Methode gibt die Liste der gesetzten Steine zurück an den Dialog
        public List<TempGem> GetPlacedGems()
        {
            var valid = new List<TempGem>();
            foreach (var g in _previewGems)
            {
                if (g.IsValid) valid.Add(g);
            }
            return valid;
        }

        protected override void OnMouseMove(GetPointMouseEventArgs e)
        {
            base.OnMouseMove(e);
            _previewGems.Clear();

            Point3d pt = e.Point;

            // 1. Projektion & Normale
            Point3d closestPointOnBrep;
            ComponentIndex ci;
            double u, v;
            Vector3d normalResult;

            bool found = _targetBrep.ClosestPoint(pt, out closestPointOnBrep, out ci, out u, out v, double.MaxValue, out normalResult);
            Point3d cp = found ? closestPointOnBrep : pt;
            Vector3d normal = found ? normalResult : Vector3d.ZAxis;

            if (found && ci.ComponentIndexType == ComponentIndexType.BrepFace)
            {
                var face = _targetBrep.Faces[ci.Index];
                if (face.OrientationIsReversed) normal.Reverse();
            }
            if (_flip) normal.Reverse();

            // 2. Positionen (Symmetrie)
            var positionsToCalc = new List<Tuple<Point3d, Vector3d>>();
            positionsToCalc.Add(new Tuple<Point3d, Vector3d>(cp, normal));

            if (_symX)
            {
                int count = positionsToCalc.Count;
                for (int i = 0; i < count; i++)
                {
                    var p = positionsToCalc[i].Item1;
                    var n = positionsToCalc[i].Item2;
                    positionsToCalc.Add(new Tuple<Point3d, Vector3d>(new Point3d(-p.X, p.Y, p.Z), new Vector3d(-n.X, n.Y, n.Z)));
                }
            }
            if (_symY)
            {
                int count = positionsToCalc.Count;
                for (int i = 0; i < count; i++)
                {
                    var p = positionsToCalc[i].Item1;
                    var n = positionsToCalc[i].Item2;
                    positionsToCalc.Add(new Tuple<Point3d, Vector3d>(new Point3d(p.X, -p.Y, p.Z), new Vector3d(n.X, -n.Y, n.Z)));
                }
            }

            // 3. Geometrie vorbereiten
            GemShapes.ShapeType type = GemShapes.ShapeType.Round;
            Enum.TryParse(_shapeName, out type);
            Curve baseCurve = GemShapes.Create(type, _sizeX, _sizeY);

            var param = new GemParameters { Table = 57.0, H1 = _sizeX * 0.14, H2 = _sizeX * 0.03, H3 = _sizeX * 0.43 };
            Brep rawGem = null;
            if (baseCurve != null) rawGem = GemBuilder.CreateGem(baseCurve, param, _sizeX);

            // Bumper (Voller Gap)
            double bumperOffset = _gap;
            Curve rawBumper = null;
            if (baseCurve != null)
            {
                var offsets = baseCurve.Offset(Plane.WorldXY, bumperOffset, 0.001, CurveOffsetCornerStyle.Sharp);
                if (offsets != null && offsets.Length > 0)
                    rawBumper = offsets[0];
                else
                {
                    rawBumper = baseCurve.DuplicateCurve();
                    double scale = (_sizeX + _gap * 2) / _sizeX;
                    rawBumper.Transform(Transform.Scale(Point3d.Origin, scale));
                }
            }

            double currentRadius = (_sizeX + _sizeY) / 4.0;

            // 4. Instanzen
            foreach (var posData in positionsToCalc)
            {
                Point3d currentP = posData.Item1;
                Vector3d currentN = posData.Item2;

                // Kollision
                bool collision = false;
                foreach (var existing in _existingGems)
                {
                    double requiredDist = currentRadius + existing.Radius + _gap;
                    if (existing.Point.DistanceTo(currentP) < requiredDist) { collision = true; break; }
                }
                if (!collision)
                {
                    foreach (var other in _previewGems)
                    {
                        double requiredDist = (currentRadius * 2) + _gap;
                        if (other.Position.DistanceTo(currentP) < requiredDist) { collision = true; break; }
                    }
                }

                bool valid = true;
                if (collision && !_allowCollision) valid = false;

                Plane plane = new Plane(currentP, currentN);

                Brep gemInstance = rawGem?.DuplicateBrep();
                gemInstance?.Transform(Transform.PlaneToPlane(Plane.WorldXY, plane));

                Curve bumperInstance = rawBumper?.DuplicateCurve();
                bumperInstance?.Transform(Transform.PlaneToPlane(Plane.WorldXY, plane));

                // Hier wird TempGem verwendet
                _previewGems.Add(new TempGem
                {
                    Position = currentP,
                    Normal = currentN,
                    Geometry = gemInstance,
                    Bumper = bumperInstance,
                    IsValid = valid,
                    IsCollision = collision
                });
            }
        }

        protected override void OnDynamicDraw(GetPointDrawEventArgs e)
        {
            foreach (var g in _previewGems)
            {
                if (g.Geometry != null)
                {
                    var bumpColor = g.IsCollision ? System.Drawing.Color.Red : System.Drawing.Color.Lime;
                    e.Display.DrawBrepShaded(g.Geometry, new DisplayMaterial(_gemColor, 0.7));
                    e.Display.DrawBrepWires(g.Geometry, System.Drawing.Color.Gray, 1);

                    if (g.Bumper != null)
                        e.Display.DrawCurve(g.Bumper, bumpColor, 2);
                }
            }
            base.OnDynamicDraw(e);
        }
    }

    // HIER KEINE KLASSENDEFINITION MEHR, DA IN EIGENER DATEI!
}