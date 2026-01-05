using Rhino.Display;
using Rhino.Geometry;
using System.Drawing;

namespace NewRhinoGold.Core
{
    public class RingPreviewConduit : DisplayConduit
    {
        private Curve _railCurve;
        private Brep[] _ringBreps;
        // Wir speichern zusätzlich die Meshes für die Vorschau
        private Mesh[] _previewMeshes;

        private Color _drawColor;
        private BoundingBox _bbox;
        private DisplayMaterial _material;

        public RingPreviewConduit()
        {
            Enabled = false;
            _drawColor = Color.Gold;
            _material = new DisplayMaterial(_drawColor, 0.0);
        }

        public void SetScene(Curve rail, Brep[] ring, Color displayColor)
        {
            _railCurve = rail;
            _ringBreps = ring;
            _drawColor = displayColor;
            _previewMeshes = null; // Reset

            if (_material.Diffuse != _drawColor)
            {
                _material = new DisplayMaterial(_drawColor, 0.0);
            }

            _bbox = BoundingBox.Unset;

            // 1. Rail Box
            if (_railCurve != null)
            {
                if (!_bbox.IsValid) _bbox = _railCurve.GetBoundingBox(true);
                else _bbox.Union(_railCurve.GetBoundingBox(true));
            }

            // 2. Ring Box & Meshing
            if (_ringBreps != null)
            {
                var meshList = new System.Collections.Generic.List<Mesh>();
                var mp = MeshingParameters.FastRenderMesh;

                foreach (var b in _ringBreps)
                {
                    if (b == null) continue;

                    if (!_bbox.IsValid) _bbox = b.GetBoundingBox(true);
                    else _bbox.Union(b.GetBoundingBox(true));

                    // FIX: Wir nutzen die statische Methode statt b.CreateMeshes()
                    var meshes = Mesh.CreateFromBrep(b, mp);
                    if (meshes != null)
                    {
                        meshList.AddRange(meshes);
                    }
                }
                _previewMeshes = meshList.ToArray();
            }
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            base.CalculateBoundingBox(e);
            if (_bbox.IsValid) e.IncludeBoundingBox(_bbox);
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            base.PostDrawObjects(e);

            // 1. Rail zeichnen
            if (_railCurve != null && _railCurve.IsValid)
            {
                e.Display.DrawCurve(_railCurve, Color.Black, 2);
            }

            // 2. Ring zeichnen (Meshes für Shading, Brep für Wires)
            if (_ringBreps != null && _ringBreps.Length > 0)
            {
                // A. Shading über die generierten Meshes (sicherer als DrawBrepShaded)
                if (_previewMeshes != null)
                {
                    foreach (var m in _previewMeshes)
                    {
                        e.Display.DrawMeshShaded(m, _material);
                    }
                }

                // B. Kanten (Wires) direkt vom Brep
                foreach (var b in _ringBreps)
                {
                    e.Display.DrawBrepWires(b, Color.FromArgb(100, 0, 0, 0), 1);
                }
            }
        }
    }
}