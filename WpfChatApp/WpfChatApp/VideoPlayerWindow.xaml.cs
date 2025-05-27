using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Windows.Threading;
using WpfChatApp.ViewModel;

namespace WpfChatApp
{
    /// <summary>
    /// VideoPlayerWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class VideoPlayerWindow : Window
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private bool _isDragging = false;

        public VideoPlayerWindow(string videoUrl)
        {
            try
            {
                InitializeComponent();
                mediaPlayer.Source = new Uri(videoUrl, UriKind.Absolute);
                mediaPlayer.Play();

                _timer.Interval = TimeSpan.FromMilliseconds(500);
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
            catch (Exception ex)
            {
                MainViewModel.Instance.SendLogToServer("ERROR", "비디오 플레이어 오류 : " + ex.Message);
            }
        }

        /// <summary>
        /// 동영상 재생
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Play();
        }

        /// <summary>
        /// 동영상 일시정지
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
        }

        /// <summary>
        /// 영상 오픈
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                progressSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            }
        }

        /// <summary>
        /// 사이드바 표시를 위한 함수
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isDragging && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                progressSlider.Value = mediaPlayer.Position.TotalSeconds;
            }
        }

        /// <summary>
        /// 영상 재생 포지션을 사이드바에 맞게 변경
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDragging)
            {
                //mediaPlayer.Position = TimeSpan.FromSeconds(progressSlider.Value);
            }
        }

        /// <summary>
        //
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            mediaPlayer.Position = TimeSpan.FromSeconds(progressSlider.Value);
        }
    }
}
