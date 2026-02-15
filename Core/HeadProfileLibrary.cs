using System;
using System.Collections.Generic;
using Eto.Drawing;
using Rhino.Geometry;
using NewRhinoGold.Helpers;

namespace NewRhinoGold.Core
{
	public class HeadProfileItem
	{
		public string Name { get; set; }
		public Image Preview { get; set; }
		public string Id => Name;
	}

	public static class HeadProfileLibrary
	{
		public const string FolderName = "Curves";

		public static List<HeadProfileItem> GetProfileItems()
		{
			var list = new List<HeadProfileItem>();
			var names = GetProfileNames();

			foreach (var name in names)
			{
				var crv = GetCurve(name);
				list.Add(new HeadProfileItem
				{
					Name = name,
					Preview = GenerateIcon(crv)
				});
			}
			return list;
		}

		public static List<string> GetProfileNames()
		{
			var defaults = new List<string> { "Round", "Square", "Rectangular", "D-Shape" };
			var custom = ProfileLoader.GetAvailableProfiles(FolderName);
			defaults.AddRange(custom);
			return defaults;
		}

		public static Curve GetCurve(string name)
		{
			switch (name)
			{
				case "Round": return new Circle(Plane.WorldXY, 0.5).ToNurbsCurve(); 
				case "Square":
					var sq = new Polyline { new Point3d(-0.5, -0.5, 0), new Point3d(0.5, -0.5, 0), new Point3d(0.5, 0.5, 0), new Point3d(-0.5, 0.5, 0), new Point3d(-0.5, -0.5, 0) };
					return sq.ToNurbsCurve();
				case "Rectangular":
					var re = new Polyline { new Point3d(-0.4, -0.6, 0), new Point3d(0.4, -0.6, 0), new Point3d(0.4, 0.6, 0), new Point3d(-0.4, 0.6, 0), new Point3d(-0.4, -0.6, 0) };
					return re.ToNurbsCurve();
			}

			if (name == "Round") return new Circle(Plane.WorldXY, 0.5).ToNurbsCurve();
			if (name == "D-Shape") return RingProfileLibrary.GetClosedProfile("D-Shape");

			var loaded = ProfileLoader.LoadProfile(name, FolderName);
			return loaded ?? new Circle(Plane.WorldXY, 0.5).ToNurbsCurve();
		}

		private static Image GenerateIcon(Curve crv)
		{
			if (crv == null) return null;
			try
			{
				int w = 24; int h = 24;
				var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgba);
				using (var g = new Graphics(bmp))
				{
					var c = crv.DuplicateCurve();
					var bbox = c.GetBoundingBox(true);
					var center = bbox.Center;

					// FIX CS0117: Point3d.Origin verwenden, nicht Vector3d.Origin
					c.Translate(Point3d.Origin - center);

					var poly = c.ToPolyline(0, 0, 0.1, 0, 0, 0, 0, 0, true);
					if (poly != null && poly.TryGetPolyline(out Rhino.Geometry.Polyline pl))
					{
						// FIX CS1061: Width/Height manuell berechnen
						double width = bbox.Max.X - bbox.Min.X;
						double height = bbox.Max.Y - bbox.Min.Y;

						float scale = 18f / (float)Math.Max(width, height);
						if (scale <= 0 || float.IsInfinity(scale)) scale = 10f;

						var pts = new List<PointF>();
						foreach (var p in pl)
							pts.Add(new PointF(w / 2f + (float)p.X * scale, h / 2f - (float)p.Y * scale));

						g.DrawLines(Colors.Black, pts.ToArray());
					}
				}
				return bmp;
			}
			catch { return null; }
		}
	}
}