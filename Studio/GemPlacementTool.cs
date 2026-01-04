using System;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using NewRhinoGold.Core;

namespace NewRhinoGold.Studio
{
    public class GemPlacementTool : GetPoint
    {
        // Schreibgeschützt machen
        private readonly Brep _gemTemplate;
        private readonly Curve _baseCurve;
        private readonly System.Drawing.Color _color;
        private readonly Brep _targetBrep;
        private readonly double _gapSize;
        private readonly Curve _gapCurve;

        private Transform _currentTransform;

        public Transform FinalTransform => _currentTransform;

        public GemPlacementTool(Brep gemTemplate, Curve baseCurve, System.Drawing.Color displayColor, Brep targetBrep = null, double gapSize = 0.0)
        {
            _gemTemplate = gemTemplate;
            _baseCurve = baseCurve;
            _color = displayColor;
            _targetBrep = targetBrep;
            _gapSize = gapSize;

            SetCommandPrompt("Position für Stein wählen (Klick zum Platzieren)");
            AcceptNothing(true);

            if (_targetBrep != null)
            {
                Constrain(_targetBrep, -1, -1, false);
            }

            if (_gapSize > 0.001 && _baseCurve != null)
            {
                var offsets = _baseCurve.Offset(Plane.WorldXY, _gapSize, 0.001, CurveOffsetCornerStyle.Round);
                if (offsets != null && offsets.Length > 0)
                    _gapCurve = offsets[0];
                else
                    _gapCurve = _baseCurve.DuplicateCurve();
            }
        }

        protected override void OnMouseMove(GetPointMouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_targetBrep != null)
            {
                // Unnötige Zuweisungen entfernt, Inline-Deklaration verwendet
                bool found = _targetBrep.ClosestPoint(e.Point, out Point3d closestPoint, out ComponentIndex ci, out double u, out double v, double.MaxValue, out Vector3d normal);

                if (found)
                {
                    if (ci.ComponentIndexType == ComponentIndexType.BrepFace)
                    {
                        var face = _targetBrep.Faces[ci.Index];
                        if (face.OrientationIsReversed) normal.Reverse();
                    }
                    _currentTransform = Transform.PlaneToPlane(Plane.WorldXY, new Plane(closestPoint, normal));
                }
                else
                {
                    _currentTransform = Transform.Translation(e.Point - Point3d.Origin);
                }
            }
            else
            {
                _currentTransform = Transform.Translation(e.Point - Point3d.Origin);
            }
        }

        protected override void OnDynamicDraw(GetPointDrawEventArgs e)
        {
            if (_gemTemplate != null && _currentTransform.IsValid)
            {
                e.Display.PushModelTransform(_currentTransform);
                e.Display.DrawBrepWires(_gemTemplate, _color, 1);

                if (_gapCurve != null)
                    e.Display.DrawCurve(_gapCurve, System.Drawing.Color.LimeGreen, 2);

                e.Display.PopModelTransform();
            }
            base.OnDynamicDraw(e);
        }

        // Statisch gemacht, da kein Instanzzugriff
        public static void Cleanup() { }
    }
}