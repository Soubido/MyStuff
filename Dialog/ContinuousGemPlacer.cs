using NewRhinoGold.Core;
using NewRhinoGold.Studio;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;

namespace NewRhinoGold.Dialog
{
	public class ContinuousGemPlacer : GetPoint
	{
		private readonly GeometryBase _gemGeo;
		private readonly Plane _srcPlane;
		private readonly ObjRef _targetRef;
		private readonly GemSmartData _sourceData;

		private Transform _currentXform;
		private double _rotationDegrees = 0.0;
		private double _gap;
		private double _gemRadius;

		private OptionDouble _optRotation;

		public ContinuousGemPlacer(GeometryBase gem, Plane srcPlane, ObjRef target, double radius, double gap, GemSmartData data)
		{
			_gemGeo = gem;
			_srcPlane = srcPlane;
			_targetRef = target;
			_gemRadius = radius;
			_gap = gap;
			_sourceData = data;
			_currentXform = Transform.Identity;

			if (_targetRef.Geometry() is Curve c) Constrain(c, false);
			else if (_targetRef.Geometry() is Brep b) Constrain(b, -1, -1, false);
			else if (_targetRef.Geometry() is Surface s) Constrain(s, false);

			SetCommandPrompt("Position Gem (Left Click: Place, Enter/Right Click: Finish)");
			AcceptNothing(true);

			_optRotation = new OptionDouble(0.0);
			AddOptionDouble("RotationAngle", ref _optRotation);
		}

		public void RunLoop(RhinoDoc doc)
		{
			while (true)
			{
				// PHASE 1: Gleiten
				_rotationDegrees = _optRotation.CurrentValue;
				GetResult res = Get();

				if (res == GetResult.Point)
				{
					Transform placeXform = _currentXform;

					// Ziel-Ebene für Rotation berechnen
					Plane clickTargetPlane = CalcTargetPlane(this.Point());

					double gapScale = 1.0;
					if (_gemRadius > 0.001) gapScale = (_gemRadius + _gap) / _gemRadius;

					Curve baseCrv = (_sourceData != null) ? _sourceData.BaseCurve : null;

					// PHASE 2: Rotieren
					// UPDATE: Wir übergeben _srcPlane.Origin als Skalierungszentrum
					var rotTool = new GemRotationTool(_gemGeo, placeXform, clickTargetPlane, baseCrv, gapScale, _srcPlane.Origin);

					GetResult rotRes = rotTool.Get();

					if (rotRes == GetResult.Point)
					{
						BakeGemWithXform(doc, rotTool.FinalTransform);
					}
				}
				else if (res == GetResult.Option)
				{
					continue;
				}
				else
				{
					break;
				}
			}
		}

		private void BakeGemWithXform(RhinoDoc doc, Transform finalXform)
		{
			if (!finalXform.IsValid) return;

			var newGeo = _gemGeo.Duplicate();
			newGeo.Transform(finalXform);

			var attr = doc.CreateDefaultAttributes();
			attr.Name = "PlacedGem";

			if (_sourceData != null)
			{
				Plane newPlane = _srcPlane;
				newPlane.Transform(finalXform);

				Curve newBaseCurve = null;
				if (_sourceData.BaseCurve != null)
				{
					newBaseCurve = _sourceData.BaseCurve.DuplicateCurve();
					newBaseCurve.Transform(finalXform);
				}

				string cut = _sourceData.CutType ?? "Unknown";
				double size = _sourceData.GemSize;
				string mat = _sourceData.MaterialName ?? "Default";
				double carat = _sourceData.CaratWeight;

				var newData = new GemSmartData(newBaseCurve, newPlane, cut, size, mat, carat);
				newData.TablePercent = _sourceData.TablePercent;
				newData.CrownHeightPercent = _sourceData.CrownHeightPercent;
				newData.GirdleThicknessPercent = _sourceData.GirdleThicknessPercent;
				newData.PavilionHeightPercent = _sourceData.PavilionHeightPercent;

				attr.UserData.Add(newData);
			}

			doc.Objects.Add(newGeo, attr);
			doc.Views.Redraw();
		}

		protected override void OnDynamicDraw(GetPointDrawEventArgs e)
		{
			Plane targetPlane = CalcTargetPlane(e.CurrentPoint);
			double rad = RhinoMath.ToRadians(_rotationDegrees);
			targetPlane.Rotate(rad, targetPlane.ZAxis, targetPlane.Origin);

			_currentXform = Transform.PlaneToPlane(_srcPlane, targetPlane);

			var material = new DisplayMaterial(System.Drawing.Color.Gold, 0.3);

			e.Display.PushModelTransform(_currentXform);
			if (_gemGeo is Brep b)
			{
				e.Display.DrawBrepShaded(b, material);
				e.Display.DrawBrepWires(b, System.Drawing.Color.DarkGoldenrod, 1);
			}
			else if (_gemGeo is Mesh m)
			{
				e.Display.DrawMeshShaded(m, material);
				e.Display.DrawMeshWires(m, System.Drawing.Color.DarkGoldenrod);
			}
			e.Display.PopModelTransform();

			// Gap Curve Zeichnen (Phase 1)
			if (_sourceData != null && _sourceData.BaseCurve != null)
			{
				Curve gapCrv = _sourceData.BaseCurve.DuplicateCurve();
				gapCrv.Transform(_currentXform);

				if (_gemRadius > 0.001)
				{
					double scaleFactor = (_gemRadius + _gap) / _gemRadius;
					// Hier skalieren wir um den neuen Mittelpunkt (Target Origin)
					var scaleXform = Transform.Scale(targetPlane.Origin, scaleFactor);
					gapCrv.Transform(scaleXform);
				}
				e.Display.DrawCurve(gapCrv, System.Drawing.Color.Cyan, 2);
			}
			else
			{
				double totalRadius = _gemRadius + _gap;
				e.Display.DrawCircle(new Circle(targetPlane, totalRadius), System.Drawing.Color.Cyan, 2);
			}

			base.OnDynamicDraw(e);
		}

		private Plane CalcTargetPlane(Point3d pt)
		{
			Vector3d normal = Vector3d.ZAxis;

			if (_targetRef.Geometry() is Curve crv)
			{
				if (crv.ClosestPoint(pt, out double t)) pt = crv.PointAt(t);
				return new Plane(pt, Vector3d.ZAxis);
			}
			else
			{
				if (_targetRef.Geometry() is Brep brep)
				{
					brep.ClosestPoint(pt, out Point3d cp, out ComponentIndex ci, out double s, out double t, 0.5, out Vector3d n);
					pt = cp; normal = n;
				}
				else if (_targetRef.Geometry() is Surface srf)
				{
					srf.ClosestPoint(pt, out double u, out double v);
					pt = srf.PointAt(u, v); normal = srf.NormalAt(u, v);
				}
				return new Plane(pt, normal);
			}
		}
	}
}