using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.IO;

namespace NewRhinoGold
{
    /// <summary>
    /// DisplayConduit für die Vorschau der erzeugten Steine.
    /// Unterstützt nun dynamische Farbänderungen.
    /// </summary>
    public class GemDisplayCond : DisplayConduit
    {
        private DisplayMaterial _material;
        private Brep[] _breps;
        private Curve[] _curves;
        private Point3d[] _points;

        // Standardfarbe (System.Drawing.Color für Kompatibilität mit RhinoCommon Draw-Methoden)
        private System.Drawing.Color _drawColor;

        private Point3d? _p1;
        private Point3d? _p2;

        public GemDisplayCond()
        {
            _material = new DisplayMaterial();
            _material.IsTwoSided = true; // Wichtig damit man den Stein auch von innen sieht falls offen

            // Initial: Standard Rhino Feedback Farbe
            _drawColor = AppearanceSettings.FeedbackColor;
        }

        public void Enable()
        {
            if (!Enabled) Enabled = true;
        }

        public void Disable()
        {
            if (Enabled) Enabled = false;
        }

        /// <summary>
        /// Setzt die Darstellungsfarbe für Wires und Kurven.
        /// </summary>
        public void SetColor(System.Drawing.Color color)
        {
            _drawColor = color;
        }

        public void setbreps(Brep[] breps)
        {
            _breps = breps;
        }

        public void setcurves(Curve[] curves)
        {
            _curves = curves;
        }

        public void setpoints(Point3d[] points)
        {
            _points = points;
        }

        public void setpoint2(Point3d p1, Point3d p2)
        {
            _p1 = p1;
            _p2 = p2;
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            if (_breps != null)
            {
                foreach (var b in _breps)
                {
                    if (b != null) e.IncludeBoundingBox(b.GetBoundingBox(true));
                }
            }
            if (_curves != null)
            {
                foreach (var c in _curves)
                {
                    if (c != null) e.IncludeBoundingBox(c.GetBoundingBox(true));
                }
            }
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            // Breps (Steine)
            if (_breps != null)
            {
                foreach (var b in _breps)
                {
                    if (b == null) continue;

                    // Zeichnet schattierte Flächen (nutzt Standard-Materialeinstellungen)
                    e.Display.DrawBrepShaded(b, _material);

                    // Zeichnet die Kanten in der gewählten Farbe
                    e.Display.DrawBrepWires(b, _drawColor);
                }
            }

            // Kurven (z.B. Rondiste Vorschau)
            if (_curves != null)
            {
                foreach (var c in _curves)
                {
                    if (c == null) continue;
                    e.Display.DrawCurve(c, _drawColor, 2);
                }
            }

            // Punkte
            if (_points != null)
            {
                foreach (var pt in _points)
                {
                    e.Display.DrawPoint(pt, _drawColor);
                }
            }

            // Linien
            if (_p1.HasValue && _p2.HasValue)
            {
                e.Display.DrawLine(_p1.Value, _p2.Value, _drawColor, 2);
            }
        }
    }
}