using System;
using System.Collections.Generic;
using Rhino.Geometry;
using System.Linq;

namespace NewRhinoGold.Core
{
	public static class RingBuilder
	{
		public static Brep[] BuildRing(double diameterMM, RingProfileSlot[] slots, bool solid)
		{
			if (diameterMM < 1.0) diameterMM = 10.0;

			// 1. RAIL erstellen (Kreis in XY, Naht unten bei 6 Uhr)
			double radius = diameterMM / 2.0;
			// Wir rotieren den Kreis um -90 Grad, damit t=0 bei 6 Uhr liegt (unten)
			var circle = new Circle(Plane.WorldXY, radius);
			circle.Rotate(-Math.PI / 2.0, Vector3d.ZAxis, Point3d.Origin);

			Curve rail = circle.ToNurbsCurve();
			rail.Domain = new Interval(0, 1.0); // Normieren 0..1

			// 2. PROFILE vorbereiten
			var sweepShapes = new List<Curve>();
			var sweepParams = new List<double>();

			// Wir loopen durch alle 36 Slots
			for (int i = 0; i < 36; i++)
			{
				var slot = slots[i];

				// Nur aktive Slots werden für den Sweep genutzt!
				// Ausnahme: Wenn nur sehr wenige aktiv sind, könnte man interpolieren, 
				// aber Rhino Sweep macht das meist gut selbst.
				if (!slot.IsActive) continue;

				// Parameter auf dem Kreis berechnen (0..1)
				// Index 0 (12 Uhr) ist gegenüber von Start (6 Uhr).
				// Logik: 6 Uhr = Index 18. 
				// Wir müssen die Indexe mappen: 
				// Index 0 (12h) -> muss t=0.5 sein?
				// Index 18 (6h) -> muss t=0.0 / 1.0 sein?
				// Lassen wir Index 0 bei 12 Uhr sein.

				double angleDeg = i * 10; // 0..350
										  // Korrektur: Index 0 ist oben. Rail Start ist unten.
										  // Also ist Index 0 bei 180 Grad vom Start entfernt.
				double angleFromStart = (angleDeg + 180) % 360;
				double t = angleFromStart / 360.0;

				// --- KURVEN TRANSFORMATION ---
				// 1. Basis holen (Zentriert auf 0,0)
				// NEU: Wir laden anhand des Namens
				Curve shape = ProfileLoader.LoadProfile(slot.ProfileName);

				// WICHTIG: Da die geladene Kurve beliebige Größe haben kann, 
				// müssen wir sie auf die gewünschte Width/Height ZWINGEN.
				BoundingBox bbox = shape.GetBoundingBox(true);
				double currentW = bbox.Max.X - bbox.Min.X;
				double currentH = bbox.Max.Y - bbox.Min.Y;

				// Faktor berechnen (Ziel / Ist)
				// Schutz gegen Division durch Null falls Kurve kaputt ist
				if (currentW < 0.001) currentW = 1;
				if (currentH < 0.001) currentH = 1;

				double scaleX = slot.Width / currentW;
				double scaleY = slot.Height / currentH;

				// 2. Skalieren (Width/Height)
				shape.Scale(0.0); // Reset scale factor? Nein, Transform nutzen.
				var scale = Transform.Scale(Plane.WorldXY, slot.Width, slot.Height, 1.0);
				shape.Transform(scale);

				// 3. Invert (Flip)
				if (slot.InvertDirection)
				{
					shape.Rotate(Math.PI, Vector3d.YAxis, Point3d.Origin); // 180 Grad Flip
				}

				// 4. Rotation (um sich selbst)
				if (Math.Abs(slot.Rotation) > 0.001)
				{
					double rad = Rhino.RhinoMath.ToRadians(slot.Rotation);
					shape.Rotate(rad, Vector3d.ZAxis, Point3d.Origin);
				}

				// 5. Position (Offset vom Mittelpunkt)
				// Verschiebung entlang Y (lokal oben/unten relativ zur Schiene)
				if (Math.Abs(slot.PositionOffset) > 0.001)
				{
					shape.Translate(0, slot.PositionOffset, 0);
				}

				// 6. Rotation V (Tilt/Banking)
				// Das machen wir am besten VOR dem Orientieren, Drehung um X-Achse
				if (Math.Abs(slot.RotationV) > 0.001)
				{
					double radV = Rhino.RhinoMath.ToRadians(slot.RotationV);
					shape.Rotate(radV, Vector3d.XAxis, Point3d.Origin);
				}

				// --- AUF RAIL LEGEN ---
				// Frame an Position t holen
				Plane railFrame;
				rail.PerpendicularFrameAt(t, out railFrame);

				// Orientieren von WorldXY auf RailFrame
				var orient = Transform.PlaneToPlane(Plane.WorldXY, railFrame);
				shape.Transform(orient);

				sweepShapes.Add(shape);
				sweepParams.Add(t);
			}

			// Fallback: Wenn keine Shapes aktiv sind (sollte nicht passieren)
			if (sweepShapes.Count == 0) return null;

			// Spezialfall geschlossener Sweep: Start und Ende müssen matchen
			// Wenn bei t=0 (Index 18) einer ist, und bei t=1 keiner, 
			// kopieren wir den von t=0 ans Ende.
			// Rhino Sweep1 Closed kümmert sich meist darum, aber saubere Daten helfen.

			// 3. SWEEP
			var sweep = new SweepOneRail();
			sweep.ClosedSweep = true;
			// Sweep Tolerance einstellen für saubere Ergebnisse
			sweep.AngleToleranceRadians = 0.01;
			sweep.SweepTolerance = 0.001;

			var breps = sweep.PerformSweep(rail, sweepShapes.ToArray(), sweepParams.ToArray());

			if (breps == null || breps.Length == 0) return null;

			// 4. SOLID / CAP
			var result = new List<Brep>();
			foreach (var b in breps)
			{
				if (solid)
				{
					// Versuchen zu schließen (CapPlanarHoles)
					var capped = b.CapPlanarHoles(0.001);
					if (capped != null) result.Add(capped);
					else result.Add(b);
				}
				else
				{
					result.Add(b);
				}
			}

			return result.ToArray();
		}
	}
}