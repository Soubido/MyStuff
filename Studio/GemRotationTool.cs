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
        private readonly Brep _gemTemplate;
        private readonly System.Drawing.Color _color;
        private readonly Transform _baseTransform; // Position & Normalen-Ausrichtung (aus Schritt 1)
        private readonly Plane _basePlane; // Die Ebene auf der wir rotieren
        
        private Transform _rotationTransform = Transform.Identity;
        private Curve _gapCurve; // Optional auch hier anzeigen

        public Transform FinalTransform => _rotationTransform * _baseTransform;

        public GemRotationTool(Brep gemTemplate, Transform baseTransform, System.Drawing.Color color, Curve gapCurve = null)
        {
            _gemTemplate = gemTemplate;
            _baseTransform = baseTransform;
            _color = color;
            _gapCurve = gapCurve;

            // Ermittle die Ebene der aktuellen Platzierung
            _basePlane = Plane.WorldXY;
            _basePlane.Transform(_baseTransform);

            SetCommandPrompt("Rotation bestimmen (Maus bewegen)");
            SetBasePoint(_basePlane.Origin, true);
            
            // Wir beschränken die Maus auf die Ebene des Steins
            Constrain(_basePlane, false);
        }

        protected override void OnMouseMove(GetPointMouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Vektor vom Zentrum zur Maus
            Vector3d vec = e.Point - _basePlane.Origin;
            if (vec.IsTiny(0.001)) return;

            // Winkel zur X-Achse der BasePlane berechnen
            // Standard Ausrichtung ist Y-Achse (bei Edelsteinen oft oben).
            // Wir nehmen an 0 Grad ist "Oben" oder X?
            // Rhino Standard: X ist 0.
            
            // Wir berechnen den Winkel relativ zur X-Achse der lokalen Ebene
            double angle = Vector3d.VectorAngle(_basePlane.XAxis, vec, _basePlane);
            
            // Rotation um Z-Achse der Ebene (Normal)
            _rotationTransform = Transform.Rotation(angle, _basePlane.ZAxis, _basePlane.Origin);
        }

        protected override void OnDynamicDraw(GetPointDrawEventArgs e)
        {
            if (_gemTemplate != null)
            {
                // Kombinierte Transformation: Erst Basis (Position), dann Rotation
                Transform total = _rotationTransform * _baseTransform;

                e.Display.PushModelTransform(total);
                e.Display.DrawBrepWires(_gemTemplate, _color, 1);
                
                if (_gapCurve != null)
                {
                    e.Display.DrawCurve(_gapCurve, System.Drawing.Color.LimeGreen, 2);
                }
                
                e.Display.PopModelTransform();
            }
            base.OnDynamicDraw(e);
        }
    }
}