using RgbFx.UI;

ApplicationConfiguration.Initialize();
Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
Application.ThreadException += (_, e) =>
    MessageBox.Show(e.Exception.ToString(), "RgbFx.UI error", MessageBoxButtons.OK, MessageBoxIcon.Error);
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown error", "RgbFx.UI fatal", MessageBoxButtons.OK, MessageBoxIcon.Error);

try
{
    Application.Run(new MainForm());
}
catch (Exception ex)
{
    MessageBox.Show(ex.ToString(), "RgbFx.UI startup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
