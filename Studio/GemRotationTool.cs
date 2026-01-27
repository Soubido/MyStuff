using System;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace NewRhinoGold.Studio
{
    public class GemRotationTool : GetPoint
    {
        private readonly GeometryBase _gemGeo;
        private readonly System.Drawing.Color _color;
        private readonly Transform _baseTransform;
        private readonly Plane _basePlane;

        private Transform _rotationTransform = Transform.Identity;
        private Curve _gapCurve;
        private double _gapScale = 1.0;

        // NEU: Das Zentrum für die Skalierung der Gap-Kurve
        private readonly Point3d _scalingCenter = Point3d.Origin;

        public Transform FinalTransform => _rotationTransform * _baseTransform;

        // --- KONSTRUKTOR 1 (Legacy / GemStudio) ---
        // Bleibt exakt wie er war -> Kompatibel mit altem Code
        public GemRotationTool(Brep gemTemplate, Transform baseTransform, System.Drawing.Color color, Curve gapCurve = null)
        {
            _gemGeo = gemTemplate;
            _baseTransform = baseTransform;
            _color = color;
            _gapCurve = gapCurve;
            _gapScale = 1.0;
            _scalingCenter = Point3d.Origin; // Standard (egal bei Scale 1.0)

            _basePlane = Plane.WorldXY;
            _basePlane.Transform(_baseTransform);
            SetupTool();
        }

        // --- KONSTRUKTOR 2 (Neu / PickGem) ---
        // UPDATE: Nimmt jetzt 'scalingCenter' entgegen
        public GemRotationTool(GeometryBase gem, Transform baseTransform, Plane rotationBasePlane, Curve gapCurve, double gapScale, Point3d scalingCenter)
        {
            _gemGeo = gem;
            _baseTransform = baseTransform;
            _color = System.Drawing.Color.Gold;
            _gapCurve = gapCurve;
            _gapScale = gapScale;
            _scalingCenter = scalingCenter; // Hier merken wir uns den Ursprung

            // Wir nehmen die übergebene Plane als Wahrheit für den Drehpunkt
            _basePlane = rotationBasePlane;

            SetupTool();
        }

        private void SetupTool()
        {
            SetCommandPrompt("Rotation bestimmen (Maus bewegen, Klick zum Setzen)");
            SetBasePoint(_basePlane.Origin, true);
            Constrain(_basePlane, false);
        }

        protected override void OnMouseMove(GetPointMouseEventArgs e)
        {
            base.OnMouseMove(e);
            Vector3d vec = e.Point - _basePlane.Origin;
            if (vec.Length < 0.001) return;

            double angle = Vector3d.VectorAngle(_basePlane.XAxis, vec, _basePlane);

            // Rotation um den exakten Origin der übergebenen Plane
            _rotationTransform = Transform.Rotation(angle, _basePlane.ZAxis, _basePlane.Origin);
        }

        protected override void OnDynamicDraw(GetPointDrawEventArgs e)
        {
            Transform total = _rotationTransform * _baseTransform;

            e.Display.PushModelTransform(total);

            if (_gemGeo is Brep b)
            {
                e.Display.DrawBrepShaded(b, new DisplayMaterial(_color, 0.5));
                e.Display.DrawBrepWires(b, _color, 1);
            }
            else if (_gemGeo is Mesh m)
            {
                e.Display.DrawMeshShaded(m, new DisplayMaterial(_color, 0.5));
                e.Display.DrawMeshWires(m, _color);
            }

            if (_gapCurve != null)
            {
                var dispCrv = _gapCurve.DuplicateCurve();

                // Gap Skalierung
                if (Math.Abs(_gapScale - 1.0) > 0.001)
                {
                    // KORREKTUR: Skalieren um das Source-Zentrum, nicht Welt-Nullpunkt
                    var scaleXform = Transform.Scale(_scalingCenter, _gapScale);
                    dispCrv.Transform(scaleXform);
                }

                e.Display.DrawCurve(dispCrv, System.Drawing.Color.Cyan, 2);
            }

            e.Display.PopModelTransform();

            e.Display.DrawLine(_basePlane.Origin, e.CurrentPoint, System.Drawing.Color.Gray);

            base.OnDynamicDraw(e);
        }
    }
}