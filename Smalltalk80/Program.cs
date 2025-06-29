using System;
using System.Windows.Forms;

class Program {
    [STAThread]
    static void Main(string[] args) {
        Console.WriteLine("Smalltalk-80 System Boot");
        StandardLibrary.Initialize();

        if (args.Contains("--gui")) {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(WidgetFactory.CreateButtonForm("Click Me"));
        } else {
            Console.WriteLine("Use --gui to launch GUI.");
        }
    }
}