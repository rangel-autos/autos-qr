namespace GeneradorQrAutos;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show($"Ocurrió un error inesperado:\n{e.Exception.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        Form1 form;
        try
        {
            form = new Form1();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo iniciar la aplicación:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(form);
    }
}
