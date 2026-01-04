using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using NewRhinoGold.Studio;

namespace NewRhinoGold.Commands
{
    public class PaveStudioCommand : Command
    {
        public override string EnglishName => "PaveStudio";
        public override Guid Id => new Guid("A1B2C3D4-E5F6-7890-1234-56789ABCDEF0");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                var dlg = new PaveStudioDlg();

                // Owner setzen (verhindert, dass der Dialog hinter Rhino verschwindet)
                var owner = RhinoEtoApp.MainWindow;
                if (owner == null && Eto.Forms.Application.Instance != null)
                {
                    owner = Eto.Forms.Application.Instance.MainForm;
                }
                dlg.Owner = owner;

                // Modeless anzeigen (Show), damit man im Viewport arbeiten kann
                dlg.Show();

                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Fehler beim Starten von PaveStudio: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}