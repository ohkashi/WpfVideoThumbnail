using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;


namespace WpfVideoThumbnail;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
	public class Thumbnail
	{
		public BitmapImage? ThumbBitmap { get; set; }
		public string? Name { get; set; }
	}

	public List<Thumbnail> ThumbList = [];

	private readonly ThumbnailGenerator thumbGen = new();

	public MainWindow()
    {
		//DataContext = this;

		InitializeComponent();
		WindowStartupLocation = WindowStartupLocation.CenterScreen;

		ThumbListbox.ItemsSource = ThumbList;
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);

		var isLightTheme = IsLightTheme();
		var source = (HwndSource)PresentationSource.FromVisual(this);
		ToggleBaseColour(source.Handle, !isLightTheme);

		// Detect when the theme changed
		source.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => {
			const int WM_SETTINGCHANGE = 0x001A;
			if (msg == WM_SETTINGCHANGE) {
				if (wParam == IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet") {
					var isLightTheme = IsLightTheme();
					ToggleBaseColour(hwnd, !isLightTheme);
				}
			}
			return IntPtr.Zero;
		});
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		thumbGen?.Dispose();
	}

	private void MenuItem_OpenVideo(object sender, RoutedEventArgs e)
	{
		var dlg = new OpenFileDialog
		{
			Title = "Select Video",
			Filter = ""
		};
		dlg.Filter = "Video Files|*.mp4;*.avi;*.mkv|All Files|*.*";
		if (dlg.ShowDialog() == true) {
			if (!thumbGen.OpenFile(dlg.FileName)) {
				return;
			}

			const int thumb_count = 6;
			const long ref_time_unit = 10000000;	// 100 nanosec.
			var duration = thumbGen.GetDuration();
			//var total_sec = (long)(duration / ref_time_unit);
			var step = (long)(duration / (ulong)thumb_count);
			long pos = 2 * ref_time_unit;

			ThumbList.Clear();
			long lastPos = 0;
			bool lastThumb = false;
			for (int i = 0; i < thumb_count; i++) {
				try {
					if (pos > (long)duration) {
						pos = (long)(duration - 10000);
						lastThumb = true;
						if (lastPos > 0 && (pos - lastPos) < ref_time_unit)
							break;
					}
					var tsec = pos / ref_time_unit;
					var _m = tsec / 60;
					var _s = tsec % 60;
					ThumbList.Add(new()
					{
						ThumbBitmap = thumbGen.CreateBitmap(pos),
						Name = $"Thumb{i} ({_m}:{_s})"
					});
					Debug.WriteLine(ThumbList[i].Name);
				} catch {
					break;
				}
				if (lastThumb)
					break;
				lastPos = pos;
				pos += step;
			}
			CollectionViewSource.GetDefaultView(ThumbList).Refresh();
		}
	}

	private void MenuItem_Exit(object sender, RoutedEventArgs e)
	{
		Application.Current.Shutdown();
	}

	private void OnThumbPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (sender is ListBoxItem lbi) {
			ThumbListbox.SelectedItem = lbi.DataContext;
		}
	}

	private void ThumbListbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		var item = e.Source as ListBox;
		if (item != null) {
			Debug.WriteLine($"Item:{item!.SelectedIndex} selected.");
			Sprite.SourceImage = ThumbList[item.SelectedIndex].ThumbBitmap;
		}
	}

	private readonly PaletteHelper _paletteHelper = new PaletteHelper();

	private void ToggleBaseColour(nint hwnd, bool isDark)
	{
		var theme = _paletteHelper.GetTheme();
		var baseTheme = isDark ? BaseTheme.Dark : BaseTheme.Light;
		theme.SetBaseTheme(baseTheme);
		_paletteHelper.SetTheme(theme);
		UseImmersiveDarkMode(hwnd, isDark);
	}

	private static bool IsLightTheme()
	{
		using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
		var value = key?.GetValue("AppsUseLightTheme");
		return value is int i && i > 0;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

	private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
	{
		if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) {
			var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
			if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985)) {
				attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
			}

			int useImmersiveDarkMode = enabled ? 1 : 0;
			return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
		}

		return false;
	}
}
