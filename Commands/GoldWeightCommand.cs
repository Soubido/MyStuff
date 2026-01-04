using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using NewRhinoGold.Dialog;

namespace NewRhinoGold.Commands
{
    public class GoldWeightCommand : Command
    {
        public override string EnglishName => "GoldWeight";

        public override Guid Id => new Guid("A5B6C7D8-E9F0-4123-8456-7890ABCDEF12");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Fix IDE0090 (new)
            var dialog = new GoldWeightDlg();

            // Fix IDE0270 (Null check) & IDE0074 (Compound assignment)
            var rhinoWindow = RhinoEtoApp.MainWindow ?? Eto.Forms.Application.Instance.MainForm;

            dialog.ShowModal(rhinoWindow);

            return Result.Success;
        }
    }
}