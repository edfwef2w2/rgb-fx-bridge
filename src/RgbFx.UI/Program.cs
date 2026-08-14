using RgbFx.UI;
using RgbFx.UI.Aura;

if (args.Any(a => string.Equals(a, "--hal-add", StringComparison.OrdinalIgnoreCase)))
    Environment.Exit(AuraHalOps.Add());
if (args.Any(a => string.Equals(a, "--hal-remove", StringComparison.OrdinalIgnoreCase)))
    Environment.Exit(AuraHalOps.Remove());

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
