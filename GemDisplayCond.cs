using System.Collections.Generic;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Drawing;

namespace NewRhinoGold.Core
{
    public class GemDisplayCond : DisplayConduit
    {
        private IEnumerable<Brep> _breps;
        private IEnumerable<Curve> _curves; // Wieder hinzugefügt

        private Color _color = Color.Gold;
        private DisplayMaterial _material;

        public GemDisplayCond()
        {
            // Initial Material erstellen
            UpdateMaterial();
        }

        // --- FEHLENDE METHODEN (FIX FÜR CS1061) ---

        // Wrapper für die Basis-Property 'Enabled'
        public void Enable()
        {
            this.Enabled = true;
        }

        public void Disable()
        {
            this.Enabled = false;
        }

        // Methode zum Setzen von Kurven (wird von HeadStudio/GemCreator benötigt)
        public void setcurves(IEnumerable<Curve> curves)
        {
            _curves = curves;
        }

        // -------------------------------------------

        public void setbreps(IEnumerable<Brep> breps)
        {
            _breps = breps;
        }

        public void SetColor(Color c)
        {
            _color = c;
            UpdateMaterial();
        }

        private void UpdateMaterial()
        {
            _material = new DisplayMaterial();
            _material.Diffuse = _color;

            // Transparenz für Breps (0.0 = solid, 1.0 = unsichtbar)
            _material.Transparency = 0.6;
            _material.Shine = 0.4;
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            // BoundingBox für Breps erweitern
            if (_breps != null)
            {
                foreach (var b in _breps)
                {
                    if (b != null) e.IncludeBoundingBox(b.GetBoundingBox(false));
                }
            }

            // BoundingBox für Kurven erweitern
            if (_curves != null)
            {
                foreach (var c in _curves)
                {
                    if (c != null) e.IncludeBoundingBox(c.GetBoundingBox(false));
                }
            }
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            // 1. Breps zeichnen (Transparent + Wires)
            if (_breps != null && _material != null)
            {
                foreach (var b in _breps)
                {
                    if (b == null) continue;

                    // Kanten zeichnen (in voller Farbe, damit man sie sieht)
                    e.Display.DrawBrepWires(b, _color, 1);

                    // Flächen transparent zeichnen
                    e.Display.DrawBrepShaded(b, _material);
                }
            }

            // 2. Kurven zeichnen (Solid)
            if (_curves != null)
            {
                foreach (var c in _curves)
                {
                    if (c == null) continue;
                    e.Display.DrawCurve(c, _color, 2); // Dicke 2 für bessere Sichtbarkeit
                }
            }
        }
    }
}