class Program {
    static void Main(string[] args) {
        Console.WriteLine("Smalltalk-80 System Boot");
        StandardLibrary.Initialize();
        Application.Run(WidgetFactory.CreateButtonForm("Click Me"));
    }
}