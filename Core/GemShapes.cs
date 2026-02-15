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
                    // KORREKTUR: Erstellung über Unit-Shape Methode
                    var pearCurve = CreateUnitPear();
                    // Skalieren auf gewünschte Maße
                    Transform scalePear = Transform.Scale(Plane.WorldXY, width, length, 1.0);
                    pearCurve.Transform(scalePear);
                    result = pearCurve;
                    break;

                case ShapeType.Heart:
                    // Optimierte Herzform über Kontrollpunkte
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

                // Seam Reset für sauberes Sweeping
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
        /// Erstellt eine normierte Tropfenform (Pear) in 1x1 Box.
        /// </summary>
        private static Curve CreateUnitPear()
        {
            // Spitze oben (0, 0.5), Boden unten (0, -0.5)
            Point3d pTip = new Point3d(0, 0.5, 0);
            Point3d pBottom = new Point3d(0, -0.5, 0);

            var cv = new Point3d[5];
            cv[0] = pTip;

            // WICHTIG FÜR SPITZE: 
            // Der zweite Punkt muss Y < 0.5 haben. 
            // Vektor (0.1, -0.15) erzwingt einen steilen Abgang -> Spitze.
            // Wäre Y=0.5, wäre es oben rund (tangential horizontal).
            cv[1] = new Point3d(0.12, 0.35, 0);

            // WICHTIG FÜR BAUCH:
            // Y = -0.15 zieht den Bauch unter die Mitte (0.0).
            cv[2] = new Point3d(0.55, -0.15, 0);

            // Überleitung zum Boden
            cv[3] = new Point3d(0.55, -0.5, 0);
            cv[4] = pBottom;

            // Rechte Hälfte erstellen
            var rightSide = Curve.CreateControlPointCurve(cv, 3);

            // Spiegeln
            var leftSide = rightSide.DuplicateCurve();
            leftSide.Transform(Transform.Mirror(Plane.WorldYZ));
            leftSide.Reverse();

            // Join erzeugt den Knick an der Spitze (G0 Continuity), da Tangenten nicht kollinear sind
            var joined = Curve.JoinCurves(new Curve[] { rightSide, leftSide });
            return joined[0];
        }

        private static Curve CreateUnitHeart()
        {
            Point3d pTip = new Point3d(0, -0.5, 0);
            Point3d pCleft = new Point3d(0, 0.25, 0);

            var cv = new Point3d[6];
            cv[0] = pCleft;
            cv[1] = new Point3d(0.1, 0.55, 0);  // Steil nach oben aus dem Cleft
            cv[2] = new Point3d(0.40, 0.55, 0); // Top Bogen
            cv[3] = new Point3d(0.65, 0.25, 0); // Breiteste Stelle
            cv[4] = new Point3d(0.40, -0.30, 0); // Flanke zur Spitze
            cv[5] = pTip;

            var rightHeart = Curve.CreateControlPointCurve(cv, 3);
            var leftHeart = rightHeart.DuplicateCurve();
            leftHeart.Transform(Transform.Mirror(Plane.WorldYZ));
            leftHeart.Reverse();

            var joined = Curve.JoinCurves(new Curve[] { rightHeart, leftHeart });
            return joined[0];
        }
    }
}