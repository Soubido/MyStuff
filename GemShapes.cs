using System;
using System.Collections.Generic;
using Rhino;
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
            Emerald,    // Octagon (Scharfe Ecken)
            Pear,       // Tropfen (Scharfe Spitze)
            Heart,      // Herz (Scharfe Spitze & Cleft)
            Marquise,   // Navette
            Cushion     // Kissen
        }

        public static string[] GetNames() => Enum.GetNames(typeof(ShapeType));

        public static Curve Create(ShapeType type, double width, double length)
        {
            double rx = width / 2.0;
            double ry = length / 2.0;

            Curve result = null;

            switch (type)
            {
                case ShapeType.Round:
                    result = new Circle(Plane.WorldXY, rx).ToNurbsCurve();
                    break;

                case ShapeType.Square:
                    result = new Rectangle3d(Plane.WorldXY, new Interval(-rx, rx), new Interval(-rx, rx)).ToNurbsCurve();
                    break;

                case ShapeType.Oval:
                    result = new Ellipse(Plane.WorldXY, rx, ry).ToNurbsCurve();
                    break;

                case ShapeType.Emerald:
                    // Scharfe Ecken durch PolylineCurve (Grad 1)
                    double chamfer = Math.Min(width, length) * 0.15;
                    var octPts = new List<Point3d>
                    {
                        new Point3d(rx - chamfer, ry, 0),
                        new Point3d(rx, ry - chamfer, 0),
                        new Point3d(rx, -ry + chamfer, 0),
                        new Point3d(rx - chamfer, -ry, 0),
                        new Point3d(-rx + chamfer, -ry, 0),
                        new Point3d(-rx, -ry + chamfer, 0),
                        new Point3d(-rx, ry - chamfer, 0),
                        new Point3d(-rx + chamfer, ry, 0),
                        new Point3d(rx - chamfer, ry, 0)
                    };
                    result = new PolylineCurve(octPts);
                    break;

                case ShapeType.Cushion:
                    var sq = new Rectangle3d(Plane.WorldXY, new Interval(-rx, rx), new Interval(-ry, ry));
                    double cornerCushion = Math.Min(width, length) * 0.20;
                    result = Curve.CreateFilletCornersCurve(sq.ToPolyline().ToNurbsCurve(), cornerCushion, 0.01, 0.1);
                    break;

                case ShapeType.Marquise:
                    // Exakte Kreis-Schnittmenge
                    if (Math.Abs(rx) < 0.001) rx = 0.001;
                    double cx = (ry * ry - rx * rx) / (2.0 * rx);
                    var arcRight = new Arc(new Point3d(0, ry, 0), new Point3d(rx, 0, 0), new Point3d(0, -ry, 0));
                    var arcLeft = new Arc(new Point3d(0, -ry, 0), new Point3d(-rx, 0, 0), new Point3d(0, ry, 0));

                    var curvesM = new List<Curve> { arcRight.ToNurbsCurve(), arcLeft.ToNurbsCurve() };
                    var joinedM = Curve.JoinCurves(curvesM, 0.001);
                    if (joinedM != null && joinedM.Length > 0) result = joinedM[0];
                    break;

                case ShapeType.Pear:
                    // KORREKTUR: Erstellung �ber Unit-Shape Methode
                    var pearCurve = CreateUnitPear();
                    // Skalieren auf gew�nschte Ma�e
                    Transform scalePear = Transform.Scale(Plane.WorldXY, width, length, 1.0);
                    pearCurve.Transform(scalePear);
                    result = pearCurve;
                    break;

                case ShapeType.Heart:
                    // Optimierte Herzform �ber Kontrollpunkte
                    var heartCurve = CreateUnitHeart();
                    Transform scaleHeart = Transform.Scale(Plane.WorldXY, width, length, 1.0);
                    heartCurve.Transform(scaleHeart);
                    result = heartCurve;
                    break;

                default:
                    result = new Circle(Plane.WorldXY, rx).ToNurbsCurve();
                    break;
            }

            if (result != null)
            {
                if (!result.IsClosed) result.MakeClosed(RhinoMath.ZeroTolerance);

                // Seam Reset f�r sauberes Sweeping
                if (type == ShapeType.Round || type == ShapeType.Oval || type == ShapeType.Pear || type == ShapeType.Heart)
                {
                    result.ChangeClosedCurveSeam(result.Domain.Min);
                    double tBottom;
                    // Versuch, den Seam nach unten zu legen
                    if (result.ClosestPoint(new Point3d(0, -1000, 0), out tBottom))
                        result.ChangeClosedCurveSeam(tBottom);
                }
            }
            return result;
        }

        /// <summary>
        /// Erstellt eine normierte Tropfenform (Pear) in 1×1 Box.
        /// Komplett als EINE geschlossene periodische Grad-3 NurbsCurve —
        /// kein Mirror, kein Join, keine Naht-Probleme.
        /// Überall mindestens G2 (innere Knoten), damit Offset() sauber funktioniert.
        /// </summary>
        private static Curve CreateUnitPear()
        {
            // Kontrollpunkte: volle Kontur, Start = Boden-Mitte.
            // Symmetrie wird manuell sichergestellt (rechts/links gespiegelte X-Werte).
            //
            //          Spitze (0, 0.50)
            //         /                \
            //   Schulter               Schulter
            //      |                      |
            //    Bauch (breiteste)       Bauch
            //      |                      |
            //   Flanke                 Flanke
            //         \                /
            //          Boden (0, -0.50)
            //
            var pts = new Point3d[]
            {
                new Point3d( 0.00, -0.50, 0),    //  0  Boden Mitte
                new Point3d( 0.30, -0.50, 0),    //  1  Boden rechts
                new Point3d( 0.48, -0.35, 0),    //  2  Flanke rechts
                new Point3d( 0.50, -0.10, 0),    //  3  Bauch rechts (breiteste Stelle)
                new Point3d( 0.40,  0.15, 0),    //  4  Schulter rechts
                new Point3d( 0.15,  0.40, 0),    //  5  Zur Spitze rechts
                new Point3d( 0.00,  0.50, 0),    //  6  Spitze (oben)
                new Point3d(-0.15,  0.40, 0),    //  7  Zur Spitze links (symmetrisch zu 5)
                new Point3d(-0.40,  0.15, 0),    //  8  Schulter links (symmetrisch zu 4)
                new Point3d(-0.50, -0.10, 0),    //  9  Bauch links (symmetrisch zu 3)
                new Point3d(-0.48, -0.35, 0),    // 10  Flanke links (symmetrisch zu 2)
                new Point3d(-0.30, -0.50, 0),    // 11  Boden links (symmetrisch zu 1)
            };

            return CreatePeriodicCurve(pts, 3);
        }

        /// <summary>
        /// Erstellt eine normierte Herzform in 1×1 Box.
        /// Komplett als EINE geschlossene periodische Grad-3 NurbsCurve —
        /// kein Mirror, kein Join → sauberes Offset() im BezelBuilder.
        /// </summary>
        private static Curve CreateUnitHeart()
        {
            // Kontrollpunkte: volle Kontur, Start = Spitze unten.
            //
            //     Cleft (0, 0.30)
            //    /   \          \
            //  Lobe-L  Lobe-R
            //   |               |
            //  Breiteste      Breiteste
            //   |               |
            //  Flanke         Flanke
            //    \             /
            //     Spitze (0, -0.50)
            //
            var pts = new Point3d[]
            {
                new Point3d( 0.00, -0.50, 0),    //  0  Spitze unten
                new Point3d( 0.18, -0.30, 0),    //  1  Flanke rechts unten
                new Point3d( 0.48, -0.02, 0),    //  2  Breiteste Stelle rechts
                new Point3d( 0.50,  0.30, 0),    //  3  Lobe-Aufstieg rechts
                new Point3d( 0.35,  0.52, 0),    //  4  Lobe-Spitze rechts (höchster Punkt)
                new Point3d( 0.12,  0.42, 0),    //  5  Lobe-Abstieg rechts zum Cleft
                new Point3d( 0.00,  0.30, 0),    //  6  Cleft (Kerbe oben Mitte)
                new Point3d(-0.12,  0.42, 0),    //  7  Lobe-Abstieg links (symmetrisch zu 5)
                new Point3d(-0.35,  0.52, 0),    //  8  Lobe-Spitze links (symmetrisch zu 4)
                new Point3d(-0.50,  0.30, 0),    //  9  Lobe-Aufstieg links (symmetrisch zu 3)
                new Point3d(-0.48, -0.02, 0),    // 10  Breiteste Stelle links (symmetrisch zu 2)
                new Point3d(-0.18, -0.30, 0),    // 11  Flanke links unten (symmetrisch zu 1)
            };

            return CreatePeriodicCurve(pts, 3);
        }

        /// <summary>
        /// Erstellt eine geschlossene periodische NurbsCurve aus Kontrollpunkten.
        /// Periodisch = die letzten (degree) Punkte wrappen auf die ersten →
        /// überall gleiche Knotenstruktur, überall mindestens G2, keine Nahtstelle.
        /// Funktioniert wie Rhinos "Periodic=Yes" Option beim Curve-Command.
        /// </summary>
        private static NurbsCurve CreatePeriodicCurve(Point3d[] pts, int degree)
        {
            int n = pts.Length;          // Anzahl der einzigartigen Kontrollpunkte
            int order = degree + 1;     // 4 bei Grad 3

            // Periodische Kurve: wir wiederholen die ersten (degree) Punkte am Ende.
            // → n + degree Kontrollpunkte insgesamt.
            int cpCount = n + degree;

            // Knotenvektor: gleichmäßig, n + degree + degree = n + 2*degree Knoten.
            // Für periodische Kurven: alle Knoten haben Multiplizität 1 → überall G(degree-1).
            int knotCount = cpCount + degree - 1;  // = n + 2*degree - 1

            var nc = new NurbsCurve(degree, cpCount);

            // Kontrollpunkte setzen: original + wrap-around
            for (int i = 0; i < cpCount; i++)
            {
                nc.Points.SetPoint(i, pts[i % n]);
            }

            // Gleichmäßiger Knotenvektor (uniform): 0, 1, 2, 3, ...
            for (int i = 0; i < knotCount; i++)
            {
                nc.Knots[i] = i;
            }

            if (nc.IsValid) return nc;
            return null;
        }
    }
}