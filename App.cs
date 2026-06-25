using System.Windows;

namespace MapaMaquinas
{
    public class App : Application
    {
        [System.STAThread]
        public static void Main()
        {
            var app = new App();
            app.Run(new MainWindow());
        }
    }
}
