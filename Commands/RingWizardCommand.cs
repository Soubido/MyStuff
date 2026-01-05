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

            var result = dlg.ShowModal(RhinoEtoApp.MainWindow);

            if (result)
            {
                // FIX: Wir holen jetzt den fertigen Ring (Breps), nicht mehr nur die Rail
                var breps = dlg.GetFinalRing();

                if (breps != null && breps.Length > 0)
                {
                    uint sn = doc.BeginUndoRecord("Create Ring Wizard");

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