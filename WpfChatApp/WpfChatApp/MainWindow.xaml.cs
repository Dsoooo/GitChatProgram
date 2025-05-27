using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
using System.Windows.Shapes;
using Newtonsoft.Json;
using WpfChatApp.Model;
using WpfChatApp.ViewModel;

namespace WpfChatApp
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private bool _isLoggingOut;

        public MainWindow(TcpClient client, UserInfo userInfo, string userList)
        {
            InitializeComponent();

            //mainWindow에서 로그인 후에 MainViewModel 객체 생성
            var vm = new MainViewModel(userInfo, client);       // ViewModel 생성            
            vm.SharedClient = client;
            this.DataContext = vm;                              // 바인딩
            CallUserList(userList, vm);                         // 사용자 목록 적용
            _viewModel = vm;
            _isLoggingOut = false;
            
        }

        /// <summary>
        /// mainViewModel 첫 생성시 사용자, 채팅 목록 List 요청
        /// </summary>
        /// <param name="json"></param>
        /// <param name="viewModel"></param>
        private async void CallUserList(string json, MainViewModel viewModel)
        {
            try
            {
                // 사용자 목록 JSON 수신 여부 판단
                if (json.StartsWith("[") && json.Contains("IdNum") && json.Contains("Nickname"))
                {
                    var userList = JsonConvert.DeserializeObject<List<UserInfo>>(json);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        viewModel.AllUsers.Clear();
                        foreach (var user in userList)
                        {
                            // 나 자신은 제외
                            if (user.IdNum != viewModel.UserInfo.IdNum)
                                viewModel.AllUsers.Add(user);
                        }
                    });
                }
            }
            catch (Exception ex)
            {            
                _viewModel.SendLogToServer("ERROR", "유저 리스트 호출 Error");
            }
        }

        /// <summary>
        /// 로그아웃
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLoggingOut = true;
                var login = new LoginWindow();
                login.Show();

                //_client?.Disconnect();
                _viewModel.Disconnection();
                this.Close();
                _viewModel.SendLogToServer("INFO", "로그아웃");
            }
            catch (Exception ex) 
            {
                _viewModel.SendLogToServer("ERROR", "LogOut Error : " + ex.Message);
            }

        }

        /// <summary>
        /// 꺼질때 클라이언트 연결 종료 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isLoggingOut) return;
            this.Logout_Click(null, null); 
        }

        /// <summary>
        /// User 목록에서 더블클릭으로 방 생성 시
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UserListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (UserListBox.SelectedItem is UserInfo selectedUser)
                {
                    var vm = this.DataContext as MainViewModel;
                    if (vm == null) return;
                    var participants = new List<int> { vm.UserInfo.IdNum, selectedUser.IdNum };

                    // 방 ID 생성 (1:1 기준)
                    string roomId = ChatRoom.CreateRoomId(participants);

                    // 기존 방이 있는지 확인
                    var existingRoom = vm.ChatRooms.FirstOrDefault(r => r.RoomId == roomId);
                    if (existingRoom == null)
                    {
                        string roomName = string.Join("", vm.UserInfo.Nickname + selectedUser.Nickname);
                        existingRoom = new ChatRoom(roomId, selectedUser.Nickname, vm.UserInfo.IdNum, "", 0);

                        vm.ChatRooms.Insert(0, existingRoom);
                        vm.SendRoomToServer(existingRoom);
                    }

                    //기존 채팅창 열기
                    vm.SelectedRoom = existingRoom;
                    vm.OpenChatRooms.Add(existingRoom);
                    var chatViewModel = new ChatViewModel(vm);
                    vm.SelectedRoom.ViewModel = chatViewModel;
                    OpenChatWindow(true, chatViewModel, existingRoom);

                    _viewModel.SendLogToServer("INFO", "UserListBox DoubleClick 방 생성");
                }

            }
            catch (Exception ex)
            {
                _viewModel.SendLogToServer("ERROR", "UserListBox DoubleClick 에러 : " + ex.Message);
            }
        }

        /// <summary>
        /// 채팅방을 눌러서 접속시
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ChatRoomListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is ListBox listBox && listBox.SelectedItem is ChatRoom selectedRoom)
                {
                    // 현재 ViewModel에서 필요한 정보 추출
                    var vm = this.DataContext as MainViewModel;
                    if (vm == null) return;

                    //기존 채팅창 열기
                    vm.SelectedRoom = selectedRoom;
                    vm.OpenChatRooms.Add(selectedRoom);
                    var chatViewModel = new ChatViewModel(vm);
                    vm.SelectedRoom.ViewModel = chatViewModel;

                    OpenChatWindow(true, chatViewModel, selectedRoom);
                    _viewModel.SendLogToServer("INFO", "ChatRoomList DoubleClick 방 생성");
                }
            }
            catch (Exception ex)
            {
                _viewModel.SendLogToServer("ERROR", "ChatRoomList DoubleClick 에러 : " + ex.Message);
            }
        }

        /// <summary>
        /// 방 생성 버튼
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CreateChatRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = this.DataContext as MainViewModel;
                if (vm == null) return;

                var selectWindow = new UserSelectionWindow(vm.AllUsers, vm.UserInfo); // 현재 사용자 포함
                if (selectWindow.ShowDialog() == true)
                {
                    var selectedUsers = selectWindow.SelectedUsers;
                    if (!selectedUsers.Any()) return;

                    //여러명 단톡인 경우 본인 포함
                    var participants = selectedUsers.Select(u => u.IdNum).ToList();
                    if (!participants.Contains(vm.UserInfo.IdNum))
                        participants.Add(vm.UserInfo.IdNum);

                    // RoomId 일관성 있게 생성
                    string roomId = ChatRoom.CreateRoomId(participants);
                    string roomName = string.Empty;

                    if (participants.Count > 2)
                        roomName = vm.UserInfo.Nickname + ", " + string.Join(", ", selectedUsers.Select(u => u.Nickname));
                    else
                        roomName = string.Join("", selectedUsers.Select(u => u.Nickname));

                    var existingRoom = vm.ChatRooms.FirstOrDefault(r => r.RoomId == roomId);
                    if (existingRoom == null)
                    {
                        var room = new ChatRoom(roomId, roomName, vm.UserInfo.IdNum, "", 0);
                        vm.SelectedRoom = existingRoom;
                        vm.ChatRooms.Insert(0, room);
                        vm.SendRoomToServer(room);
                        existingRoom = room;
                    }

                    //기존 채팅창 열기
                    vm.OpenChatRooms.Add(existingRoom);
                    vm.SelectedRoom = existingRoom;
                    var chatViewModel = new ChatViewModel(vm);
                    vm.SelectedRoom.ViewModel = chatViewModel;
                    OpenChatWindow(true, chatViewModel, existingRoom);
                    _viewModel.SendLogToServer("INFO", "새 채팅 버튼 Click");
                }
            }
            catch (Exception ex)
            {
                _viewModel.SendLogToServer("ERROR", "새 채팅 버튼 에러 : " + ex.Message);
            }
        }

        /// <summary>
        /// 닉네임 변경 선택 이벤트
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChangeNickName_Clck(object sender, RoutedEventArgs e)
        {
            // 현재 보이는 상태 확인 후 토글
            if (NickTextBox.Visibility == Visibility.Collapsed)
            {
                // 편집 모드로 전환
                NickTextBlock.Visibility = Visibility.Collapsed;
                NickTextBox.Visibility = Visibility.Visible;
                NickTextBox.Focus();
                BtnNickName.Content = "저장";
            }
            else
            {
                // 편집 종료 모드
                NickTextBlock.Visibility = Visibility.Visible;
                NickTextBox.Visibility = Visibility.Collapsed;
                BtnNickName.Content = "변경";
                ChangeUserInfo_Click (sender, e);
            }
        }

        /// <summary>
        /// 닉네임 변경
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChangeUserInfo_Click(object sender, RoutedEventArgs e)
        {
            string newNickname = NickTextBox.Text.Trim();

            if (!string.IsNullOrEmpty(newNickname))
            {
                // 닉네임 변경 로직 (예: 서버로 전송 또는 ViewModel 업데이트)
                _viewModel.UserInfo.Nickname = newNickname;
                _viewModel.SendUserUpdateToServer();
                MessageBox.Show("닉네임이 변경되었습니다.");
            }
            else
            {
                MessageBox.Show("닉네임을 입력해주세요.");
            }
        }
        
        /// <summary>
        /// SerachTextBox Clear
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearSearchText_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;           
        }

        /// <summary>
        /// 마우스 우클릭으로 채팅방 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OpenChatRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ChatListBox.SelectedItem is ChatRoom selectedRoom)
                {
                    // 현재 ViewModel에서 필요한 정보 추출
                    var vm = this.DataContext as MainViewModel;
                    if (vm == null) return;

                    // 중복 방지
                    if (vm.OpenChatRooms.Contains(selectedRoom))
                        return;
                    vm.OpenChatRooms.Add(selectedRoom);
                    //기존 채팅창 열기
                    vm.SelectedRoom = selectedRoom;
                    var chatViewModel = new ChatViewModel(vm);
                    vm.SelectedRoom.ViewModel = chatViewModel;

                    _viewModel.SendLogToServer("INFO", "채팅방 열기(마우스 우클릭) Click");
                    OpenChatWindow(true, chatViewModel, selectedRoom);
                }
            }
            catch(Exception ex)  
            {
                _viewModel.SendLogToServer("ERROR", "채팅방 열기(마우스 우클릭) 에러 : " + ex.Message);
            }
        }

        /// <summary>
        /// 채팅방 여는 공통 함수
        /// </summary>
        /// <param name="exisiting"></param>
        /// <param name="chatViewModel"></param>
        /// <param name="room"></param>
        private async void OpenChatWindow(bool exisiting, ChatViewModel chatViewModel, ChatRoom room)
        {
            //기존 ChatRoom의 경우 메시지 History 호출
            if (exisiting) { await chatViewModel.LoadChatHistoryAsync(); }
            var chatWindow = new ChatWindow(chatViewModel);

            chatWindow.Closed += (s, e2) =>
            {
                if (_viewModel.OpenChatRooms.Contains(room))
                {
                    _viewModel.OpenChatRooms.Remove(room);
                }
            };
            chatWindow.Owner = this;
            chatWindow.Show();
        }


        /// <summary>
        /// 채팅방 나가기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LeaveChatRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ChatListBox.SelectedItem is ChatRoom selectedRoom)
                {
                    var vm = DataContext as MainViewModel;
                    if (vm != null)
                    {
                        vm.ChatRooms.Remove(selectedRoom);
                        //var chatViewModel = new ChatViewModel(vm.SelectedRoom, vm.UserInfo);
                        await vm.SendDeleteRoomToServer(selectedRoom);
                        _viewModel.SendLogToServer("INFO", "채팅방 나가기(마우스 우클릭) Click");
                    }
                }
            }
            catch (Exception ex)
            {
                _viewModel.SendLogToServer("ERROR", "채팅방 나가기 에러 : " + ex.Message);
            }
        }

    }
}
