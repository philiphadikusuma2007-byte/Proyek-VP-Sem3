using System.Windows.Forms.DataVisualization.Charting;

namespace SuryaMandiriPrint;

/// <summary>Membuat grafik (Chart control) untuk tampilan form dan untuk disisipkan ke laporan FastReport.</summary>
public static class ChartHelper
{
    public static Chart Buat(string judul, string namaSeri, SeriesChartType tipe,
                             IList<(string label, double nilai)> data, Color warna, string judulY)
    {
        var chart = new Chart { BackColor = Color.White, Dock = DockStyle.Fill };

        var area = new ChartArea("area") { BackColor = Color.White };
        area.AxisX.MajorGrid.Enabled = false;
        area.AxisX.Interval = Math.Max(1, (int)Math.Ceiling(data.Count / 14.0));
        area.AxisX.LabelStyle.Angle = data.Count > 8 ? -45 : 0;
        area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
        area.AxisY.Title = judulY;
        area.AxisY.LabelStyle.Format = "N0";
        chart.ChartAreas.Add(area);

        chart.Titles.Add(new Title(judul, Docking.Top, new Font("Segoe UI", 11f, FontStyle.Bold), Color.FromArgb(52, 40, 26)));

        var s = new Series(namaSeri)
        {
            ChartType = tipe,
            Color = warna,
            BorderWidth = 3,
            IsValueShownAsLabel = data.Count <= 16,
            LabelFormat = "N0",
            XValueType = ChartValueType.String
        };
        if (tipe == SeriesChartType.Line)
        {
            s.MarkerStyle = MarkerStyle.Circle;
            s.MarkerSize = 7;
        }
        foreach (var (label, nilai) in data) s.Points.AddXY(label, nilai);
        chart.Series.Add(s);

        chart.Legends.Add(new Legend("legend") { Docking = Docking.Bottom });
        return chart;
    }

    /// <summary>Render grafik menjadi gambar PNG (dipakai sebagai PictureObject di FastReport).</summary>
    public static Image KeGambar(Chart chart, int lebar = 900, int tinggi = 360)
    {
        chart.Width = lebar;
        chart.Height = tinggi;
        using var ms = new MemoryStream();
        chart.SaveImage(ms, ChartImageFormat.Png);
        ms.Position = 0;
        using var asli = Image.FromStream(ms);
        return new Bitmap(asli);
    }
}
