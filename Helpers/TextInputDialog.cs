using System;
using Eto.Drawing;
using Eto.Forms;

namespace NewRhinoGold.Helpers
{
    public class TextInputDialog : Dialog<string>
    {
        private TextBox _txtInput;

        public TextInputDialog(string title, string defaultText)
        {
            Title = title;
            ClientSize = new Size(300, 120);
            Resizable = false;
            Topmost = true;
            // Dialog startposition zentriert auf Owner setzen wir beim Aufruf (ShowModal)

            _txtInput = new TextBox { Text = defaultText };

            var btnOk = new Button { Text = "Save" };
            btnOk.Click += (s, e) => Close(_txtInput.Text);

            var btnCancel = new Button { Text = "Cancel" };
            btnCancel.Click += (s, e) => Close(null);

            // Layout erstellen
            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };

            layout.AddRow(new Label { Text = "Profile Name:" });
            layout.AddRow(_txtInput);
            layout.AddRow(null); // Spacer (drückt Buttons nach unten)

            // Buttons rechtsbündig anordnen
            var buttonStack = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Items = { btnCancel, btnOk }
            };
            layout.AddRow(buttonStack);

            Content = layout;

            // Fokus auf Textfeld setzen, sobald Dialog angezeigt wird
            Shown += (s, e) => _txtInput.Focus();
        }
    }
}