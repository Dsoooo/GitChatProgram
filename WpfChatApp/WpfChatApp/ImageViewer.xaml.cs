using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfAnimatedGif;
using WpfChatApp.ViewModel;

namespace WpfChatApp
{
    /// <summary>
    /// ImageViewer.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ImageViewer : Window
    {
        #region variable
        //휠 한번당 10%씩 이동
        private const double ZoomFactor = 0.1;

        #endregion

        #region properties
        #endregion

        #region constuctor
        public ImageViewer(string imagePath)
        {
            InitializeComponent();
            if(imagePath == "pack://application:,,,/WpfChatApp;component/Resources/noimage.jpg")
            {
                BigImage.Source = new BitmapImage(new Uri(imagePath));
            }
            else
            {
                LoadImage(imagePath);
            }

        }
        #endregion

        #region methods

        /// <summary>
        /// 이미지 로드
        /// http:// 웹 스트리밍 되는 이미지를 다 비동기식으로 다 다운로드 후 보여줌
        /// </summary>
        private async void LoadImage(string imagePath)
        {
            try
            {               
                using (HttpClient client = new HttpClient())
                {

                    var gifBytes = await client.GetByteArrayAsync(imagePath);
                    var bitmap = new BitmapImage();
                    if (gifBytes != null)
                    {
                        using (var stream = new MemoryStream(gifBytes))
                        {
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = stream;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            ImageBehavior.SetAnimatedSource(BigImage, bitmap);
                        }
                    }
                  
                }
            }
            catch (Exception ex)
            {
                imagePath = "pack://application:,,,/WpfChatApp;component/Resources/noimage.jpg";
                BigImage.Source = new BitmapImage(new Uri(imagePath));
                MainViewModel.Instance.SendLogToServer("ERROR", "이미지 로딩 오류 : " + ex.Message);
                MessageBox.Show("이미지 로딩 오류: " + ex.Message);
            }
        }

        #endregion

        #region events

        /// <summary>
        /// 창이 열리면 포커스
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus();
        }

        /// <summary>
        /// ESC 누르면 창 닫기 Event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private void BigImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scaleTransform = (ScaleTransform)BigImage.RenderTransform;

            //휠 위로 굴림 = 확대
            if (e.Delta > 0)
            {
                scaleTransform.ScaleX += ZoomFactor;
                scaleTransform.ScaleY += ZoomFactor;
            }
            else if (e.Delta < 0) //휠 아래로 굴림 = 축소
            {
                scaleTransform.ScaleX = Math.Max(0.1, scaleTransform.ScaleX - ZoomFactor);
                scaleTransform.ScaleY = Math.Max(0.1, scaleTransform.ScaleY - ZoomFactor);
            }
        }
        #endregion
    }
}
