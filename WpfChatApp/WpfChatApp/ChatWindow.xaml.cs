using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfChatApp.Model;
using WpfChatApp.ViewModel;

namespace WpfChatApp
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ChatWindow : Window
    {
        #region variable

        //private MainViewModel ViewModel => (MainViewModel)DataContext;
        private ChatViewModel _viewModel;

        #endregion

        #region properties
        #endregion

        #region constructor

        public ChatWindow(ChatViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            this.DataContext = _viewModel;

            //채팅방 열때 안읽은 메시지 목록 초기화
            if (viewModel.Room != null)
            {
                viewModel.Room.UnReadCount = 0;
            }

            string roomId = _viewModel.Room.RoomId.ToString();
            _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            _viewModel.SendLog("INFO", roomId + " 채팅방 오픈");
        }

        #endregion

        #region methods

        /// <summary>
        /// 메시지를 입력했을때 ChatBoxList의 스크롤이 자동으로 가장 아래로 이동
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                //랜더링이 완료 된 이 후 호출되도록 수정
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ChatListBox.Items.Count > 0)
                    {
                        ChatListBox.ScrollIntoView(ChatListBox.Items[ChatListBox.Items.Count - 1]);
                    }
                }));
            }
        }

        #endregion

        #region events

        /// <summary>
        /// TextBox 내에서 키보드 입력 시 Event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // Ctrl + Enter => 줄바꿈 허용
                        var textBox = sender as System.Windows.Controls.TextBox;
                        if (textBox != null)
                        {
                            int caretIndex = textBox.CaretIndex;
                            textBox.Text = textBox.Text.Insert(caretIndex, Environment.NewLine);
                            textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                        }
                    }
                    else
                    {
                        // Enter만 => 메시지 전송
                        if (_viewModel.SendMessageCommand.CanExecute(null))
                        {
                            _viewModel.SendMessageCommand.Execute(null);
                            e.Handled = true; // 기본 엔터 입력 막기
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _viewModel.SendLog("ERROR", "TextBox Enter Error" + ex.Message);
            }
        }

        /// <summary>
        /// 이미지 파일 클릭시 새로운 viewer로 이미지 파일 크게 보는 Event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var image = sender as Image;
                if (image != null)
                {
                    string imagePath = image.Source.ToString();

                    // 새 창 열기
                    ImageViewer viewer = new ImageViewer(imagePath);
                    viewer.ShowDialog();
                    _viewModel.SendLog("INFO", "이미지 Click");
                }
            }
            catch (Exception ex)
            {
                _viewModel.SendLog("ERROR", "이미지 뷰어 Error" + ex.Message);
            }
        }

        /// <summary>
        /// 파일 선택 마우스 클릭시 다운로드
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Document_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(sender is FrameworkElement element && element.DataContext is ChatMessage message)
            {
                try
                {
                    _viewModel.SendLog("INFO", "파일 Click");

                    string url = message.Content;
                    if (string.IsNullOrWhiteSpace(url))
                        return;

                    string fileName = message.FileName;
                    if (string.IsNullOrWhiteSpace(fileName))
                        fileName = "downloaded_file";

                    string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);

                    // 이미 존재하는 경우 삭제
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);

                    using (var client = new System.Net.WebClient())
                    {
                        await Task.Run(() => client.DownloadFile(url, tempPath));  // 동기 다운로드를 Task로 래핑
                    }

                    // 파일 열기 (기본 앱으로)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _viewModel.SendLog("ERROR", "파일 Open 에러 : " + ex.Message);
                    MessageBox.Show("파일 다운로드 또는 열기에 실패했습니다.\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 이미지 없는 경우 기본 파일 표시
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                image.Source = new BitmapImage(new Uri("Resources/noimage.jpg", UriKind.Relative));
            }
        }

        /// <summary>
        /// 이미지뷰어가 기본 선택되도록 설정
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SuppressItemSelection(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
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

        /// <summary>
        /// 동영상 썸네일 클릭 이벤트
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoThumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is StackPanel panel && panel.DataContext is ChatMessage msg)
                {
                    var player = new VideoPlayerWindow(msg.Content);
                    player.Show();
                    _viewModel.SendLog("INFO", "동영상 썸네일 Click");
                }
            }
            catch (Exception ex)
            {
                _viewModel.SendLog("ERROR", "동영상 썸네일 Click Error : " + ex.Message);
            }
        }

        #endregion

    }
}
