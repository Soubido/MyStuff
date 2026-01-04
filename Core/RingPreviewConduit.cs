using Rhino.Display;
using Rhino.Geometry;
using System.Drawing;

namespace NewRhinoGold.Core
{
	public class RingPreviewConduit : DisplayConduit
	{
		private Brep[] _geometry;
		private BoundingBox _bbox;

		public RingPreviewConduit()
		{
			// Standardmäßig aus
			Enabled = false;
		}

		public void SetGeometry(Brep[] breps)
		{
			_geometry = breps;
			if (_geometry != null && _geometry.Length > 0)
			{
				_bbox = BoundingBox.Empty;
				foreach (var b in _geometry) _bbox.Union(b.GetBoundingBox(true));
			}
			else
			{
				_bbox = BoundingBox.Unset;
			}
		}

		protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
		{
			base.CalculateBoundingBox(e);
			if (_bbox.IsValid)
			{
				e.IncludeBoundingBox(_bbox);
			}
		}

		protected override void PostDrawObjects(DrawEventArgs e)
		{
			base.PostDrawObjects(e);

			if (_geometry == null || _geometry.Length == 0) return;

			// Farbe: Standard Grau/Gold (ohne Texturen, wie gewünscht)
			var material = new DisplayMaterial(Color.Gray, 0.5);

			foreach (var b in _geometry)
			{
				// Wireframe (Linien) in Schwarz für Kontrast
				e.Display.DrawBrepWires(b, Color.Black, 1);

				// Shaded (Fläche)
				e.Display.DrawBrepShaded(b, material);
			}
		}
	}
}