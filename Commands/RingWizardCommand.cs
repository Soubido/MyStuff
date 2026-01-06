using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using NewRhinoGold.Wizard;

namespace NewRhinoGold.Commands
{
    public class RingWizardCommand : Command
    {
        public override string EnglishName => "RingWizard";
        public override Guid Id => new Guid("77889900-AABB-CCDD-EEFF-1234567890AB");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var dlg = new RingWizardDlg();

            // WICHTIG: Show() statt ShowModal()
            // Dadurch bleiben die Viewports bedienbar.
            dlg.Show();

            return Result.Success;
        }
    }
}