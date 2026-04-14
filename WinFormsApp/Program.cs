namespace DataFusionArena.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Capturar excepciones no manejadas del hilo UI (como crashes del Chart)
        // para que no cierren la aplicación silenciosamente.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (sender, e) =>
        {
            // Solo mostrar mensaje — NO cerrar la app
            MessageBox.Show(
                $"Error interno recuperable:\n{e.Exception.Message}\n\n" +
                $"",
                "Data Fusion Arena – Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        };

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
