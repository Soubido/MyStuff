using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using NewRhinoGold.Studio;

namespace NewRhinoGold.Commands
{
	public class CutterStudioCommand : Command
	{
		public override string EnglishName => "CutterStudio";
		public override Guid Id => new Guid("77777777-7777-7777-7777-777777777777");

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var dlg = new CutterStudioDlg();
			// Jetzt klappt das, weil dlg ein Dialog<bool> ist
			dlg.ShowSemiModal(doc, RhinoEtoApp.MainWindow);
			return Result.Success;
		}
	}
}