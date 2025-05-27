using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
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
using Newtonsoft.Json;
using WpfChatApp.Model;

namespace WpfChatApp
{
    /// <summary>
    /// CreateAccountWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CreateAccountWindow : Window
    {
        private TcpClient _client;
        private string nameText = "이름";
        private string idText = "아이디";
        private string nickNameText = "닉네임";
        private bool isCorrect = true;              //가입자 정보 올바르게 작성 했는지 체크
        private bool isIdAvailable = false;         //아이디 중복 체크를 통과했는지 체크

        public CreateAccountWindow()
        {
            InitializeComponent();
            
        }

        //창 종료시 client가 열려 있다면 종료
        public void Dispose()
        {
            _client?.Close();
        }

        /// <summary>
        /// 회원 가입 버트
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {              
                if(IdBox.Text == idText || IdBox.Text.Length <= 0)
                {
                    IdBox.Focus();
                    isCorrect = false;
                }
                else if (PwBox.Password.Length <= 0)
                {
                    PwBox.Focus();
                    isCorrect = false;
                }
                else if (NameBox.Text == nameText)
                {
                    NameBox.Focus();
                    isCorrect = false;
                }
                else if (NicknameBox.Text == nickNameText)
                {
                    NicknameBox.Focus();
                    isCorrect = false;
                }

                if(!isCorrect)
                {
                    MessageBox.Show("가입자 정보가 올바르지 않습니다.", "회원가입 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!isIdAvailable)
                {
                    MessageBox.Show("아이디 중복 체크를 해주세요.", "회원가입 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (PwBox.Password != PwCheckBox.Password)
                {
                    MessageBox.Show("비밀번호가 일치하지 않습니다.", "회원가입 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _client = new TcpClient("127.0.0.1", 9000);
                var user = new UserInfo
                {
                    Id = IdBox.Text,
                    Nickname = NicknameBox.Text,
                    Name = NameBox.Text,
                    Password = PwBox.Password
                };

                string json = JsonConvert.SerializeObject(user);
                byte[] data = Encoding.UTF8.GetBytes("REGISTER:" + json + "\n");

                var stream = _client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);

                var reader = new StreamReader(stream, Encoding.UTF8);
                string result = await reader.ReadLineAsync();

                if (result == "SUCCESS")
                {
                    
                    MessageBox.Show("가입 성공! 로그인 해주세요.", "회원가입 성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
                else
                {
                    MessageBox.Show("이미 가입된 아이디 입니다.", "회원가입 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (SocketException ex)
            {
                MessageBox.Show("서버와 연결 할 수 없습니다.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (_client != null) { _client.Close(); }
            }
            catch (Exception ex) {
                MessageBox.Show("회원 가입 오류" + ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (_client != null) { _client.Close(); }
            }

        }

        /// <summary>
        /// 아이디 중복 확인 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void UserExists_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _client = new TcpClient("127.0.0.1", 9000);
                string json = JsonConvert.SerializeObject(IdBox.Text);
                byte[] data = Encoding.UTF8.GetBytes("IDCHECK:" + json + "\n");

                var stream = _client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);

                var reader = new StreamReader(stream, Encoding.UTF8);
                string result = await reader.ReadLineAsync();

                if (result == "ID_EXISTS")
                {
                    MessageBox.Show("아이디가 중복입니다.", "회원가입 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    isIdAvailable = true;  
                    
                    MessageBox.Show("가입 가능한 아이디입니다.", "회원가입", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                    
            }
            catch (Exception ex) { }
        }

        /// <summary>
        /// 아이디박스의 Text가 변경되면 중복체크를 다시 하도록 변경
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IdBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            isIdAvailable = false;
        }

        /// <summary>
        /// 텍스트 박스에 포커스가 가면 텍스트를 지워줌
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Placeholder_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null && (tb.Text == idText || tb.Text == nickNameText || tb.Text == nameText))
            {
                tb.Text = "";
                tb.Foreground = Brushes.Black;
            }
        }

        /// <summary>
        /// 포커스가 사라지면 다시 채워줌
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Placeholder_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null && string.IsNullOrWhiteSpace(tb.Text))
            {
                if (tb == IdBox) tb.Text = idText;
                else if (tb == NicknameBox) tb.Text = nickNameText;
                else if (tb == NameBox) tb.Text = nameText;

                tb.Foreground = Brushes.Gray;
            }
        }

        //ESC 누르면 창 닫기 Event
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        // pwBox에 Palcehold 기능이 없어서 따로 구현
        #region passwrodBox
        /// <summary>
        /// 비밀번호 박스 이벤트 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PwBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PwBox.Password))
                PwPlaceholder.Visibility = Visibility.Hidden;
        }

        private void PwBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PwBox.Password))
                PwPlaceholder.Visibility = Visibility.Visible;
        }

        private void PwBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PwPlaceholder.Visibility = string.IsNullOrEmpty(PwBox.Password)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        // 비밀번호 확인 박스 이벤트
        private void PwCheckBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PwCheckBox.Password))
                PwCheckPlaceholder.Visibility = Visibility.Hidden;
        }

        private void PwCheckBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PwCheckBox.Password))
                PwCheckPlaceholder.Visibility = Visibility.Visible;
        }

        private void PwCheckBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PwCheckPlaceholder.Visibility = string.IsNullOrEmpty(PwCheckBox.Password)
                ? Visibility.Visible
                : Visibility.Hidden;
        }
        #endregion



    }
}
