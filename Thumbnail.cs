using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using Vortice.MediaFoundation;

namespace WpfVideoThumbnail
{
    class ThumbnailGenerator : IDisposable
	{
		private IMFSourceReader? srcReader;
		private int frameWidth, frameHeight;
		private bool isTopDown = false;
		private System.Windows.Rect? frameRect;

		public void Dispose()
		{
			srcReader?.Dispose();
		}

		public bool OpenFile(string filePath)
		{
			srcReader?.Dispose();

			using IMFAttributes attributes = MediaFactory.MFCreateAttributes(1);
			attributes.Set(SourceReaderAttributeKeys.EnableVideoProcessing, true);
			try {
				srcReader = MediaFactory.MFCreateSourceReaderFromURL(filePath, attributes);
			} catch (Exception ex) {
				Debug.WriteLine(ex);
				return false;
			}
			return SelectVideoStream();
		}

		public ulong GetDuration()
		{
			if (srcReader == null)
				return 0;
			var prop = srcReader.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration);
			if (prop.ElementType == SharpGen.Runtime.Win32.VariantElementType.ULong)
				return (ulong)prop.Value;
			return 0;
		}

		public BitmapImage? CreateBitmap(long pos)
		{
			if (srcReader == null)
				return null;
			srcReader.SetCurrentPosition(pos);
			var flags = SourceReaderControlFlag.None;
			IMFSample? sample = null;
			while (true) {
				SourceReaderFlag streamFlags;
				try {
					sample = srcReader.ReadSample(SourceReaderIndex.FirstVideoStream, flags,
						out var index, out streamFlags, out var timestamp);
				} catch (Exception ex) {
					Debug.WriteLine(ex);
					break;
				}
				if (streamFlags.HasFlag(SourceReaderFlag.EndOfStream))
					break;
				if (streamFlags.HasFlag(SourceReaderFlag.CurrentMediaTypeChanged))
					GetVideoFormat();
				break;
			}
			if (sample != null && sample.Count > 0) {
				var pitch = 4 * frameWidth;
				using IMFMediaBuffer buff = sample.ConvertToContiguousBuffer();
				buff.Lock(out var pData, out var maxLength, out var cbData);
				Debug.Assert(cbData == (pitch * frameHeight));
				var bitmapSource = BitmapSource.Create(frameWidth, frameHeight, 96, 96,
					System.Windows.Media.PixelFormats.Bgr32, null, pData, cbData, pitch);
				buff.Unlock();
				sample.Dispose();
				JpegBitmapEncoder encoder = new();
				MemoryStream ms = new();
				BitmapImage image = new();
				encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
				encoder.Save(ms);
				bitmapSource = null;
				/*ms.Position = 0;
				using FileStream file = new("temp.jpg", FileMode.Create, System.IO.FileAccess.Write);
				{
					byte[] bytes = new byte[ms.Length];
					ms.Read(bytes, 0, (int)ms.Length);
					file.Write(bytes, 0, bytes.Length);
				}*/
				ms.Position = 0;
				image.BeginInit();
				image.CacheOption = BitmapCacheOption.OnLoad;
				image.StreamSource = ms;
				image.EndInit();
				ms.Close();
				return image;
			}
			return null;
		}

		private bool SelectVideoStream()
		{
			if (srcReader == null)
				return false;

			using IMFMediaType mediaType = MediaFactory.MFCreateMediaType();
			mediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
			mediaType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);

			srcReader.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, mediaType);
			srcReader.SetStreamSelection(SourceReaderIndex.FirstVideoStream, true);
			return GetVideoFormat();
		}

		private bool GetVideoFormat()
		{
			if (srcReader == null)
				return false;

			try {
				IMFMediaType mediaType = srcReader.GetCurrentMediaType(SourceReaderIndex.FirstVideoStream);
				if (mediaType == null)
					return false;
				if (mediaType.GetGUID(MediaTypeAttributeKeys.Subtype) != VideoFormatGuids.Rgb32)
					return false;

				MediaFactory.MFGetAttributeSize(mediaType, MediaTypeAttributeKeys.FrameSize, out var width, out var height);
				frameWidth = (int)width;
				frameHeight = (int)height;

				var stride = MediaFactory.MFGetAttributeUInt32(mediaType, MediaTypeAttributeKeys.DefaultStride);
				isTopDown = (stride > 0);

				var idKey = MediaTypeAttributeKeys.PixelAspectRatio;
				MediaFactory.MFGetAttributeRatio(mediaType, idKey, out var numerator, out var denominator);
				if (numerator != denominator) {
					frameRect = CorrectAspectRatio(frameWidth, frameHeight, (int)numerator, (int)denominator);
				} else {
					frameRect = new(0, 0, frameWidth, frameHeight);
				}
			} catch (Exception ex) {
				Debug.WriteLine(ex);
				return false;
			}
			return true;
		}

		private System.Windows.Rect CorrectAspectRatio(int width, int height, int numerator, int denominator)
		{
			if (numerator != 1 || denominator != 1) {
				if (numerator > denominator) {
					width = width * numerator / denominator;
				} else if (numerator < denominator) {
					height = height * denominator / numerator;
				}
			}
			return new(0, 0, width, height);
		}
	}
}
