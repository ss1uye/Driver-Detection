using System.Text;

namespace HwOps.Agent;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return SelfTest.Run(args[1]);
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(args[1]);
                    File.WriteAllText(Path.Combine(args[1], "selftest.error.txt"), ex.ToString(), Encoding.UTF8);
                }
                catch
                {
                    // 自检失败时不再抛出，退出码已能说明问题
                }

                return 1;
            }
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }
}
