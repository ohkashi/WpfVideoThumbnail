using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace WpfVideoThumbnail
{
    /// <summary>
    /// Interaction logic for SpriteControl.xaml
    /// </summary>
    public partial class SpriteControl : UserControl
    {
		public SpriteControl()
		{
			InitializeComponent();
			DataContext = this;
		}

		public BitmapImage? SourceImage
		{
			get { return srcImage; }
			set { srcImage = value;
				OnContentChanged(ContentImage.Source, srcImage!);
				ContentImage.Source = srcImage;
			}
		}

		private BitmapImage? srcImage = null;

		protected override void OnContentChanged(object oldContent, object newContent)
		{
			base.OnContentChanged(oldContent, newContent);
			Storyboard? sb = PlayShake as Storyboard;
			sb?.Begin();
		}
	}
}
