using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using NewRhinoGold.Wizard;
// NewRhinoGold.Core wird hier nicht mehr zwingend gebraucht, 
// da der Dialog die Breps direkt liefert.

namespace NewRhinoGold.Commands
{
	public class RingWizardCommand : Command
	{
		public override string EnglishName => "RingWizard";
		public override Guid Id => new Guid("77889900-AABB-CCDD-EEFF-1234567890AB");

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var dlg = new RingWizardDlg();

			// Dialog öffnen
			var result = dlg.ShowModal(RhinoEtoApp.MainWindow);

			// Wenn User "OK" klickt (result == true)
			if (result)
			{
				// FEHLERBEHEBUNG:
				// Anstatt die Einzelteile abzufragen, holen wir uns direkt
				// die fertige Geometrie vom Dialog.
				var breps = dlg.GetFinalGeometry();

				if (breps != null)
				{
					uint sn = doc.BeginUndoRecord("Create Ring");
					foreach (var b in breps)
					{
						doc.Objects.AddBrep(b);
					}
					doc.EndUndoRecord(sn);

					doc.Views.Redraw();
					return Result.Success;
				}
			}
			return Result.Cancel;
		}
	}
}