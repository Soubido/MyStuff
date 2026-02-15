using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.DocObjects;
using NewRhinoGold.Core;

namespace NewRhinoGold.Commands
{
	public class EngraveRingCommand : Command
	{
		public override string EnglishName => "EngraveRing";

		// Eindeutige ID
		public override Guid Id => new Guid("E1F2A3B4-5678-9012-3456-789ABCDEF012");

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			// 1. Objekt auswählen (Solid Brep)
			var goRing = new GetObject();
			goRing.SetCommandPrompt("Select Object to engrave (Ring/Plate)");
			goRing.GeometryFilter = ObjectType.Brep;
			goRing.EnablePreSelect(true, true);
			goRing.Get();

			if (goRing.CommandResult() != Result.Success) return goRing.CommandResult();

			var ringObj = goRing.Object(0);
			var ringBrep = ringObj.Brep();

			if (ringBrep == null) return Result.Failure;

			if (!ringBrep.IsSolid)
			{
				RhinoApp.WriteLine("Warning: Object is not a closed solid. Engraving might fail.");
			}

			// 2. Kurven auswählen
			doc.Objects.UnselectAll();

			var goCrv = new GetObject();
			goCrv.SetCommandPrompt("Select Closed Curves on surface");
			goCrv.GeometryFilter = ObjectType.Curve;
			goCrv.EnablePreSelect(true, true);

			// FIX CS1061: OptionDouble nutzen wir später mit CurrentValue
			OptionDouble optDepth = new OptionDouble(1.0, 0.1, 5.0);
			goCrv.AddOptionDouble("Depth", ref optDepth);

			while (true)
			{
				goCrv.GetMultiple(1, 0);

				if (goCrv.CommandResult() == Result.Cancel) return Result.Cancel;

				if (goCrv.Result() == GetResult.Option)
				{
					continue; // Tiefe wurde geändert
				}

				break;
			}

			if (goCrv.CommandResult() != Result.Success) return goCrv.CommandResult();

			var curves = new List<Curve>();
			foreach (var objRef in goCrv.Objects())
			{
				var c = objRef.Curve();
				if (c != null && c.IsClosed) curves.Add(c);
			}

			if (curves.Count == 0)
			{
				RhinoApp.WriteLine("No valid closed curves selected.");
				return Result.Cancel;
			}

			// 3. NEU: Zentrum bestimmen (Auto oder Manuell)
			// Standard: BoundingBox Center (gut für Ringe)
			Point3d center = ringBrep.GetBoundingBox(true).Center;

			var gp = new GetPoint();
			gp.SetCommandPrompt("Pick focal center point (Enter for Object Center)");
			gp.AcceptNothing(true); // Erlaubt Enter für Default
			gp.SetBasePoint(center, true);

			// Visualisierungslinie vom Objektzentrum zum Mauszeiger
			gp.DynamicDraw += (sender, args) =>
			{
				args.Display.DrawLine(ringBrep.GetBoundingBox(true).Center, args.CurrentPoint, System.Drawing.Color.Gray);
			};

			GetResult res = gp.Get();

			if (res == GetResult.Point)
			{
				center = gp.Point(); // Benutzerdefinierter Punkt
			}
			else if (res == GetResult.Nothing)
			{
				// Enter gedrückt -> Bleibe bei BBox Center
			}
			else
			{
				return Result.Cancel;
			}

			// FIX CS1061: Zugriff über CurrentValue
			double depthVal = optDepth.CurrentValue;

			RhinoApp.WriteLine($"Engraving {curves.Count} curves with depth {depthVal}mm...");

			uint sn = doc.BeginUndoRecord("Engrave Ring");

			try
			{
				// Builder aufrufen mit explizitem Zentrum
				Brep engravedRing = EngravingBuilder.EngraveRing(ringBrep, curves, depthVal, center);

				if (engravedRing != null && engravedRing.IsValid)
				{
					doc.Objects.Replace(ringObj.ObjectId, engravedRing);
					RhinoApp.WriteLine("Engraving successful.");
				}
				else
				{
					RhinoApp.WriteLine("Engraving failed (Result invalid or null).");
					return Result.Failure;
				}
			}
			catch (Exception ex)
			{
				RhinoApp.WriteLine($"Error during engraving: {ex.Message}");
				return Result.Failure;
			}
			finally
			{
				doc.EndUndoRecord(sn);
			}

			doc.Views.Redraw();
			return Result.Success;
		}
	}
}