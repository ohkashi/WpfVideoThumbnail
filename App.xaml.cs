using System.Configuration;
using System.Data;
using System.Windows;
using Vortice.MediaFoundation;

namespace WpfVideoThumbnail;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	void App_Startup(object sender, StartupEventArgs e)
	{
		try {
			MediaFactory.MFStartup();
		} catch (Exception ex) {
			MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			Environment.Exit(-1);
		}

		var mainWnd = new MainWindow();
		mainWnd.Show();
	}
}
