using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
	public static class GemShapes
	{
		public enum ShapeType
		{
			Round,
			Oval,
			Square,     // Princess
			Emerald,    // Rechteck
			Pear,       // Tropfen
			Heart,
			Marquise,   // Navette
			Cushion     // Kissen
		}

		public static string[] GetNames() => Enum.GetNames(typeof(ShapeType));

		public static Curve Create(ShapeType type, double width, double length)
		{
			// width = X-Achse
			// length = Y-Achse

			// Radien
			double rx = width / 2.0;
			double ry = length / 2.0;

			Curve result = null;

			switch (type)
			{
				case ShapeType.Round:
					// Ignoriert length, nutzt width für perfekten Kreis
					result = new Circle(Plane.WorldXY, rx).ToNurbsCurve();
					break;

				case ShapeType.Square:
					// Princess: Quadrat (nutzt width)
					result = new Rectangle3d(Plane.WorldXY, new Interval(-rx, rx), new Interval(-rx, rx)).ToNurbsCurve();
					break;

				case ShapeType.Oval:
					result = new Ellipse(Plane.WorldXY, rx, ry).ToNurbsCurve();
					break;

				case ShapeType.Emerald:
					// Rechteck mit Fasen (Cut Corners)
					var rect = new Rectangle3d(Plane.WorldXY, new Interval(-rx, rx), new Interval(-ry, ry));
					var poly = rect.ToPolyline();
					// Fase proportional zur kleineren Seite
					double corner = Math.Min(width, length) * 0.15;
					result = Curve.CreateFilletCornersCurve(poly.ToNurbsCurve(), corner, 0.01, 0.1);
					break;

				case ShapeType.Cushion:
					// Rechteck stark verrundet
					var sq = new Rectangle3d(Plane.WorldXY, new Interval(-rx, rx), new Interval(-ry, ry));
					double cornerCushion = Math.Min(width, length) * 0.25;
					result = Curve.CreateFilletCornersCurve(sq.ToPolyline().ToNurbsCurve(), cornerCushion, 0.01, 0.1);
					break;

				case ShapeType.Marquise:
					// Navette über Bögen
					// Wir konstruieren es so, dass es width/length erfüllt
					// Arc durch 3 Punkte: (0, ry), (rx, 0), (0, -ry)
					Point3d p1 = new Point3d(0, ry, 0);
					Point3d p2 = new Point3d(rx, 0, 0);
					Point3d p3 = new Point3d(0, -ry, 0);

					var arc1 = new Arc(p1, p2, p3);
					var arc2 = new Arc(p1, new Point3d(-rx, 0, 0), p3);

					var curves = new List<Curve> { arc1.ToNurbsCurve(), arc2.ToNurbsCurve() };
					var joined = Curve.JoinCurves(curves);
					if (joined != null && joined.Length > 0) result = joined[0];
					break;

				case ShapeType.Pear:
					// Tropfen
					// Unten: Halbkreis (Ellipse-Teil wenn nicht 1:1?) -> Wir nutzen InterpCurve für organische Form
					// Keypoints anpassen an rx/ry
					// Zentrum unten bei ca -ry * 0.5 ?
					// Spitze bei +ry

					// Vereinfachte Keypoint Logik angepasst an Bounding Box
					Point3d tip = new Point3d(0, ry, 0);
					double bottomY = -ry;
					double bellyY = -ry * 0.3; // Bauchigste Stelle

					var pts = new Point3d[]
					{
						tip,
						new Point3d(rx * 0.6, ry * 0.5, 0),
						new Point3d(rx, bellyY, 0),
						new Point3d(0, bottomY, 0),
						new Point3d(-rx, bellyY, 0),
						new Point3d(-rx * 0.6, ry * 0.5, 0),
						tip
					};
					result = Curve.CreateInterpolatedCurve(pts, 3, CurveKnotStyle.Chord);
					break;

				case ShapeType.Heart:
					// Herz angepasst an Box
					var hPts = new Point3d[]
					{
						new Point3d(0, ry * 0.3, 0), // Notch
                        new Point3d(rx * 0.5, ry, 0), // Bogen Oben
                        new Point3d(rx, ry * 0.4, 0), // Breiteste
                        new Point3d(0, -ry, 0), // Spitze
                        new Point3d(-rx, ry * 0.4, 0),
						new Point3d(-rx * 0.5, ry, 0),
						new Point3d(0, ry * 0.3, 0)
					};
					result = Curve.CreateInterpolatedCurve(hPts, 3, CurveKnotStyle.Chord);
					break;

				default:
					result = new Circle(Plane.WorldXY, rx).ToNurbsCurve();
					break;
			}

			if (result != null)
			{
				if (!result.IsClosed) result.MakeClosed(0.001);
			}

			return result;
		}
	}
}