using System;
using Rhino;
using Rhino.Commands;
using NewRhinoGold.Reporting;

namespace NewRhinoGold.Commands
{
    public class GemReportCommand : Command
    {
        public override string EnglishName => "GemReport";
        public override Guid Id => new Guid("98765432-ABCD-EF01-2345-67890ABCDEF1");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var dlg = new GemReportDlg();
            dlg.Owner = Rhino.UI.RhinoEtoApp.MainWindow;
            dlg.Show(); // Modeless
            return Result.Success;
        }
    }
}