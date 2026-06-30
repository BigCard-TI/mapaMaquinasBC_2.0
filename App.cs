using System.Windows;
using MapaMaquinas.Views;

namespace MapaMaquinas
{
    public class App : Application
    {
        [System.STAThread]
        public static void Main()
        {
            var app = new App();

            var login = new JanelaLogin();
            var loginOk = login.ShowDialog();

            // Usuário fechou a janela de login (X ou Alt+F4) sem autenticar
            if (loginOk != true || !login.LoginOk)
            {
                app.Shutdown();
                return;
            }

            app.Run(new MainWindow());
        }
    }
}
