using System.Windows.Forms;

public static class WidgetFactory {
    public static Form CreateButtonForm(string label) {
        var form = new Form {
            Width = 300,
            Height = 200,
            Text = "Smalltalk Widget"
        };

        var button = new Button {
            Text = label,
            Top = 50,
            Left = 100,
            Width = 100
        };
        button.Click += (s, e) => MessageBox.Show("Button clicked!");

        form.Controls.Add(button);
        return form;
    }
}