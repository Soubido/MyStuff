using System;
using NewRhinoGold.BezelStudio;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace NewRhinoGold.Commands
{
    public class BezelStudioCommand : Command
    {
        public override string EnglishName => "BezelStudio";
        public override Guid Id => new Guid("11223344-5566-7788-9900-AABBCCDDEEFF");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var dlg = new BezelStudioDlg();
            dlg.Show(); // Modeless
            return Result.Success;
        }
    }
}