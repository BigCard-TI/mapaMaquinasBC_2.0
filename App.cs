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

            // CRÍTICO: o padrão do WPF é ShutdownMode.OnLastWindowClose.
            // Como a JanelaLogin é a primeira janela aberta (via ShowDialog),
            // fechá-la — mesmo com sucesso — faz o WPF entender que "a última
            // janela fechou" e desliga o Application ANTES do MainWindow abrir.
            // OnExplicitShutdown desativa esse comportamento: só fecha quando
            // chamarmos Shutdown() explicitamente (login cancelado) ou quando
            // o MainWindow for fechado pelo usuário.
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var login = new JanelaLogin();
            var loginOk = login.ShowDialog();

            // Usuário fechou a janela de login (X, Alt+F4 ou Cancelar) sem autenticar
            if (loginOk != true || !login.LoginOk)
            {
                app.Shutdown();
                return;
            }

            var main = new MainWindow();

            // Agora que sabemos que o MainWindow é a janela principal,
            // restauramos o comportamento padrão: fechar o MainWindow
            // encerra a aplicação normalmente.
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.MainWindow   = main;

            app.Run(main);
        }
    }
}
