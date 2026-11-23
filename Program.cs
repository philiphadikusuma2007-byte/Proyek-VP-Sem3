namespace SuryaMandiriPrint;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppConfig.Muat();

        // Login -> Main -> (Logout kembali ke Login) ; tutup jendela utama = keluar aplikasi
        while (true)
        {
            using var login = new FormLogin();
            if (login.ShowDialog() != DialogResult.OK) break;

            using var main = new FormMain();
            main.ShowDialog();
            if (!main.Logout) break;
        }
    }
}
