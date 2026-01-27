using System;
using Rhino;
using Rhino.Commands;
using System.Runtime.InteropServices;

namespace NewRhinoGold.Commands
{
    [Guid("88776655-4433-2211-1100-AABBCCDDEEFF")] // Eindeutige ID
    public class PickGemCommand : Command
    {
        public override string EnglishName => "PickGem";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Dialog instanziieren und nicht-modal (schwebend) anzeigen
            var dlg = new NewRhinoGold.Dialog.PickGemDlg();
            dlg.Show();
            return Result.Success;
        }
    }
}