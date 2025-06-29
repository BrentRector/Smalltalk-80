using System;
using System.Linq;
using System.Windows.Forms;

public class Program {
    [STAThread]
    public static void Main(string[] args) {
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