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
        private IEnumerable<Curve> _curves;

        private Color _color = Color.Gold;
        private DisplayMaterial _material;

        public GemDisplayCond()
        {
            UpdateMaterial();
        }

        public void Enable()
        {
            this.Enabled = true;
        }

        public void Disable()
        {
            this.Enabled = false;
        }

        // C#-konforme Methodennamen
        public void SetCurves(IEnumerable<Curve> curves)
        {
            _curves = curves;
        }

        public void SetBreps(IEnumerable<Brep> breps)
        {
            _breps = breps;
        }

        // Abwärtskompatibilität: alte Aufrufe funktionieren weiterhin
        public void setcurves(IEnumerable<Curve> curves) => SetCurves(curves);
        public void setbreps(IEnumerable<Brep> breps) => SetBreps(breps);

        public void SetColor(Color c)
        {
            _color = c;
            UpdateMaterial();
        }

        private void UpdateMaterial()
        {
            _material = new DisplayMaterial();
            _material.Diffuse = _color;
            _material.Transparency = 0.6;
            _material.Shine = 0.4;
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            if (_breps != null)
            {
                foreach (var b in _breps)
                {
                    if (b != null) e.IncludeBoundingBox(b.GetBoundingBox(false));
                }
            }

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
            if (_breps != null && _material != null)
            {
                foreach (var b in _breps)
                {
                    if (b == null) continue;
                    e.Display.DrawBrepWires(b, _color, 1);
                    e.Display.DrawBrepShaded(b, _material);
                }
            }

            if (_curves != null)
            {
                foreach (var c in _curves)
                {
                    if (c == null) continue;
                    e.Display.DrawCurve(c, _color, 2);
                }
            }
        }
    }
}
