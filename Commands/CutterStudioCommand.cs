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
            try
            {
                var dlg = new CutterStudioDlg();

                // KORREKTUR: 'ShowSemiModal' entfernt. 
                // Stattdessen Owner setzen und .Show() nutzen.
                var owner = RhinoEtoApp.MainWindow;
                if (owner == null && Eto.Forms.Application.Instance != null)
                {
                    owner = Eto.Forms.Application.Instance.MainForm;
                }
                dlg.Owner = owner;

                dlg.Show(); // Modeless Show für Form
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting Cutter Studio: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}