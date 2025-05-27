using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
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
using log4net;
using Newtonsoft.Json;
using WpfChatApp.Model;

namespace WpfChatApp
{
    /// <summary>
    /// LoginWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LoginWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public LoginWindow()
        {
            InitializeComponent();

            
        }

        /// <summary>
        /// 로그인 버튼
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient("127.0.0.1", 9000);
                var user = new UserInfo
                {
                    Id = this.idBox.Text,
                    Password = this.pwBox.Password,
                    Nickname = string.Empty,
                    Name = string.Empty,
                };

                //클래스를 json 문자열로 변경해서 서버로 전송
                string json = JsonConvert.SerializeObject(user);
                byte[] data = Encoding.UTF8.GetBytes("LOGIN:" + json + "\n");

                var stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);

                var reader = new StreamReader(stream, Encoding.UTF8);
                string result = await reader.ReadLineAsync();

                if (result == "ID_NOT_EXISTS")
                {
                    MessageBox.Show("아이디 확인", "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                    client.Close(); //로그인 실패시 client를 닫아 리소스 낭비를 줄임
                }
                else if (result == "PASSWORD_WRONG")
                {
                    MessageBox.Show("비밀번호 확인", "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                    client.Close();
                }
                else
                {
                    //user 정보 받아오기
                    if (result.StartsWith("USER:"))
                    {
                        string jsonUser = result.Substring("USER:".Length);
                        user = JsonConvert.DeserializeObject<UserInfo>(jsonUser);
                    }

                    //사용자 목록 리스트 받아옴
                    string userList = string.Empty;
                    string userListJson = await reader.ReadLineAsync();
                    if (userListJson.StartsWith("USERLIST:"))
                    {
                        userList = userListJson.Substring("USERLIST:".Length);
                    }

                    //MessageBox.Show("로그인 성공! : " + user.Nickname);

                    MainWindow main = new MainWindow(client, user, userList);
                    main.Show();
                    this.Close();
                }
            }
            catch (SocketException ex)
            {
                MessageBox.Show("서버와 연결 할 수 없습니다.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (client != null) { client.Close(); }
            }
            catch (Exception ex)
            {
                //log.Error("로그인 에러 : " + ex.Message);
                MessageBox.Show("로그인 에러" + ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (client != null) { client.Close(); }
            }
        }

        /// <summary>
        /// 회원 가입 창
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            CreateAccountWindow createAccountWindow = new CreateAccountWindow();
            createAccountWindow.Owner = this;
            createAccountWindow.Show();
        }

    }
}
