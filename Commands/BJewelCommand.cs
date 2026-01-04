using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using NewRhinoGold.Dialog;

namespace NewRhinoGold.Commands
{
    public class BJewelCommand : Command
    {
        public override string EnglishName => "BJewel";
        public override Guid Id => new Guid("ABC12345-1111-2222-3333-999999999999");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Öffnet das Panel (oder schließt es, wenn sichtbar - Toggle-Verhalten kann man prüfen)
            var panelId = MainToolbarDlg.PanelId;

            if (!Panels.IsPanelVisible(panelId))
            {
                Panels.OpenPanel(panelId);
            }
            else
            {
                // Optional: Schließen, wenn man den Befehl nochmal tippt (Toggle)
                // Panels.ClosePanel(panelId); 

                // Oder einfach Fokus geben:
                Panels.OpenPanel(panelId);
            }

            return Result.Success;
        }
    }
}