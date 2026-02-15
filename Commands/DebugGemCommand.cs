using System;
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.DocObjects;
using Rhino.UI;
using Eto.Forms; // Wichtig für MessageBox
using NewRhinoGold.Core;
using NewRhinoGold.Helpers;

namespace NewRhinoGold.Commands
{
	// FIX: Hier stand nur "Command". Jetzt steht da "Rhino.Commands.Command", um den Konflikt zu lösen.
	public class DebugGemCommand : Rhino.Commands.Command
	{
		public override string EnglishName => "DebugGemInfo";
		public override Guid Id => new Guid("99999999-9999-9999-9999-999999999999");

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var go = new GetObject();
			go.SetCommandPrompt("Wähle einen Stein zur Diagnose");
			go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh;
			go.Get();
			if (go.CommandResult() != Result.Success) return go.CommandResult();

			var obj = go.Object(0).Object();
			if (obj == null) return Result.Failure;

			RhinoApp.WriteLine("--- DIAGNOSE START ---");
			RhinoApp.WriteLine($"Objekt ID: {obj.Id}");

			// 1. Prüfe auf SmartData (Direkter Zugriff)
			var smartData = obj.Geometry.UserData.Find(typeof(GemSmartData)) as GemSmartData;

			if (smartData != null)
			{
				RhinoApp.WriteLine("[OK] GemSmartData GEFUNDEN!");
				RhinoApp.WriteLine($" - Plane Origin: {smartData.GemPlane.Origin}");
				RhinoApp.WriteLine($" - Plane Normal: {smartData.GemPlane.Normal}");
				RhinoApp.WriteLine($" - Curve vorhanden: {(smartData.BaseCurve != null ? "Ja" : "Nein")}");

				if (smartData.BaseCurve != null)
				{
					var bbox = smartData.BaseCurve.GetBoundingBox(true);
					RhinoApp.WriteLine($" - Curve Center: {bbox.Center}");
				}
			}
			else
			{
				RhinoApp.WriteLine("[FEHLER] KEINE GemSmartData gefunden!");
			}

			// 2. Prüfe was der Helper sieht (Simulation HeadStudio)
			RhinoApp.WriteLine("--- HELPER TEST (HeadStudio Logik) ---");
			if (RhinoGoldHelper.TryGetGemData(obj, out var c, out var p, out var s))
			{
				RhinoApp.WriteLine("[Result] Helper war erfolgreich.");
				RhinoApp.WriteLine($" - Helper Plane Origin: {p.Origin}");

				// Liegt die Plane am Nullpunkt?
				if (p.Origin.DistanceTo(Rhino.Geometry.Point3d.Origin) < 0.001)
				{
					RhinoApp.WriteLine("!!! WARNUNG: Plane liegt auf Welt-Nullpunkt (0,0,0) !!!");
				}
				else
				{
					RhinoApp.WriteLine("Plane liegt korrekt im Raum.");
				}
			}
			else
			{
				RhinoApp.WriteLine("[Result] Helper konnte KEINE Daten extrahieren.");
			}

			RhinoApp.WriteLine("--- DIAGNOSE ENDE ---");

			// Ausgabe für bessere Lesbarkeit
			string msg = smartData != null ? "SmartData: JA\n" : "SmartData: NEIN\n";
			// Helper Test Info hinzufügen
			bool helperSuccess = RhinoGoldHelper.TryGetGemData(obj, out _, out var p2, out _);
			msg += $"Helper Plane: {(helperSuccess ? p2.Origin.ToString() : "Error")}";

			// MessageBox explizit nutzen
			MessageBox.Show(msg, "Gem Diagnose", MessageBoxButtons.OK);

			return Result.Success;
		}
	}
}