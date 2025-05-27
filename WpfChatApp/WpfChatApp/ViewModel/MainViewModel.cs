using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Web.ModelBinding;
using System.Windows;
using System.Windows.Input;
using log4net;
using Newtonsoft.Json;
using WpfChatApp.Model;
using WpfChatApp.Servieces;
using WpfChatApp.Socket;

namespace WpfChatApp.ViewModel
{
    public class MainViewModel : Notifier
    {
        #region variable

        public static MainViewModel Instance { get; private set; }

        private ChatClientSocket _client;
        private string _userId = string.Empty;
        private UserInfo _userInfo;
        private ChatRoom _selectedRoom;                               // 현재 선택된 채팅방
        private TcpClient _sharedClient;
        private static readonly ILog log = LogManager.GetLogger(typeof(MainViewModel));
        private string _searchText;
        private ObservableCollection<UserInfo> _allUsersBackup;                       //전체 유저 백업 리스트

        #endregion

        #region properties

        public TcpClient SharedClient { get; set; }
        //public ObservableCollection<UserInfo> AllUsers { get; set; }
        public ObservableCollection<ChatRoom> ChatRooms { get; set; }
        public ObservableCollection<ChatMessage> AllMessages { get; set; }
        public ObservableCollection<ChatRoom> OpenChatRooms { get; set; }   

        private ObservableCollection<UserInfo> _allUsers;
        public ObservableCollection<UserInfo> AllUsers
        {
            get => _allUsers;
            set
            {
                _allUsers = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 넘겨 받은 UserInfo 데이터를 Xaml에서 사용할 수 있게 View Model에 UserInfo 자체를 바인딩 속성으로 노출시킴
        /// </summary>
        public UserInfo UserInfo
        {
            get => _userInfo;
            set 
            { 
                _userInfo = value;
                OnPropertyChanged(); 
            }
        }

        public ChatRoom SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                OnPropertyChanged();
                //UpdateFilteredMessages(); // 방 변경 시 메시지 갱신
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    FilterUsers();
                }
            }
        }

        #endregion

        #region constuctors

        /// <summary>
        /// Viewmodel 생성자
        /// </summary>
        /// <param name="userInfo"></param> 
        /// <param name="client"></param> 로그인 시 사용된 client 재사용 하기 위해 변수로 넘겨 받음
        public MainViewModel(UserInfo userInfo, TcpClient client)
        {
            Instance = this;
            _userInfo = userInfo;
            ChatRooms = new ObservableCollection<ChatRoom>();
            OpenChatRooms = new ObservableCollection<ChatRoom>();
            _allUsers = new ObservableCollection<UserInfo>();
            _allUsersBackup = _allUsers;
            AllMessages = new ObservableCollection<ChatMessage>();
            _client = new ChatClientSocket(OnMessageReceived);
            _client.UseExistingConnection(client);
        }

        #endregion

        #region methods

        /// <summary>
        /// 양쪽에 client 각각 사용시 loop 충돌 발생 -> 메인모델이 수신받아서 chatroom에 뿌려주는 형태로 수정
        /// </summary>
        /// <param name="message"></param>
        private void OnMessageReceived(string json)
        {
            if (json.StartsWith("ROOMLIST:"))
            {
                string message = json.Substring("ROOMLIST:".Length);
                var rooms = JsonConvert.DeserializeObject<List<ChatRoom>>(message);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChatRooms.Clear();
                    foreach (var room in rooms)
                    {
                        ChatRooms.Add(room);
                    }
                });
            }
            else if (json.StartsWith("USERLIST:"))
            {
                // 사용자 목록 같은 전역 처리
            }
            else if (json.StartsWith("MESSAGE:"))
            {
                //var message = JsonConvert.DeserializeObject<ChatMessage>(json.Substring("MESSAGE:".Length));
                string withoutPrefix = json.Substring("MESSAGE:".Length);
                int roomIndex = withoutPrefix.IndexOf("ROOM:");

                string messageJson = withoutPrefix.Substring(0, roomIndex);
                string roomJson = withoutPrefix.Substring(roomIndex + "ROOM:".Length);

                var message = JsonConvert.DeserializeObject<ChatMessage>(messageJson);
                var room = JsonConvert.DeserializeObject<ChatRoom>(roomJson);

                //Message 클래스에 SenderNick 매핑 -> 채팅창에서 Sender을 닉네임으로 보여줌
                var senderUser = AllUsers.FirstOrDefault(u => u.IdNum == message.Sender);
                if (senderUser != null)
                    message.SenderNick = senderUser.Nickname;
                else
                    message.SenderNick = UserInfo.Nickname;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var newRoom = ChatRooms.FirstOrDefault(r => r.RoomId == message.RoomId);
                    if (newRoom == null)
                    {
                        ChatRooms.Add(room);
                    }
                    ChageRoomProperty(message);
                    MessageToRoom(message);                
                });
            }
            else if (json.StartsWith("MESSAGES:"))
            {
                var list = JsonConvert.DeserializeObject<List<ChatMessage>>(json.Substring("MESSAGES:".Length));
                if (list.Count == 0) return;
                var roomId = list[0].RoomId;
                var room = ChatRooms.FirstOrDefault(r => r.RoomId == roomId);

                //UI 스레드 충돌을 막기 위한 DIspatcher 사용
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (room?.ViewModel != null)
                    {
                        DateTime? lastDate = null;
                        foreach (var msg in list)
                        {
                            msg.IsMine = msg.Sender == _userInfo.IdNum;

                            var senderUser = AllUsers.FirstOrDefault(u => u.IdNum == msg.Sender);
                            if (senderUser != null)
                                msg.SenderNick = senderUser.Nickname;
                            else
                            {
                                if (msg.IsMine)
                                {
                                    msg.SenderNick = "나";
                                }
                                else
                                    msg.SenderNick = UserInfo.Nickname;
                            }

                            if (lastDate == null || msg.Time.Date != lastDate.Value.Date)
                            {
                                // 날짜 구분 메시지 삽입
                                room.ViewModel.Messages.Add(new ChatMessage
                                {
                                    ContentType = MessageType.DateSeparator,
                                    Time = msg.Time // 날짜 표시용
                                });
                                lastDate = msg.Time.Date;
                            }

                            room.ViewModel.Messages.Add(msg);
                        }
                    }
                });
            }
            else if(json.StartsWith("DELETE_ROOM:")) {
                string message = json.Substring("DELETE_ROOM:".Length);
                var room = JsonConvert.DeserializeObject<ChatRoom>(message);

                OpenChatRooms.Remove(room);
            }
        }

        /// <summary>
        /// 채팅방 순서 변경 함수
        /// </summary>
        /// <param name="message"></param>
        private void ChageRoomProperty(ChatMessage message)
        {
            //메시지가 수신된 경우 방 순서 변경
            var room = ChatRooms.FirstOrDefault(r => r.RoomId == message.RoomId);
            if (room != null)
            {
                if (message.ContentType == MessageType.Image)
                {
                    room.LastMessage = "이미지";
                }
                else if (message.ContentType == MessageType.Video)
                {
                    room.LastMessage = "비디오";
                }
                else if (message.ContentType == MessageType.Document)
                {
                    room.LastMessage = "문서";
                }
                else
                {
                    room.LastMessage = message.Content;
                }
                
                room.LastMessageDate = DateTime.Now;
            }

            ChatRooms.Remove(room);
            ChatRooms.Insert(0, room);

            //현재 열려있는 방이 아닌 경우 카운트 추가
            //if (SelectedRoom?.RoomId != room.RoomId)
            var openRoom = OpenChatRooms.FirstOrDefault(r => r.RoomId == message.RoomId);
            if (openRoom == null) 
            {
                room.UnReadCount++;
            }
        }

        /// <summary>
        /// MainViewModel에서 받은 데이터를 각각의 ChatViewModel로 메시지 전달 함수
        /// </summary>
        /// <param name="message"></param>
        private void MessageToRoom(ChatMessage message)
        {
            var room = ChatRooms.FirstOrDefault(r => r.RoomId == message.RoomId);
            if (room?.ViewModel != null)
            {
                room.ViewModel.ReceivedMessage(message);
            }       
        }

        /// <summary>
        /// 메시지를 서버로 전달
        /// </summary>
        /// <param name="message"></param>
        public async void SendMessageToServer(ChatMessage message)
        {
            await _client.SendAsync("MESSAGE:" + JsonConvert.SerializeObject(message));
        }

        /// <summary>
        /// User 정보 업데이트 서버로 전달
        /// </summary>
        public async void SendUserUpdateToServer()
        {
            await _client.SendAsync("USER_UPDATE:" + JsonConvert.SerializeObject(_userInfo));
        }

        /// <summary>
        /// 클라이언트에서 발생한 LOG 정보를 서버로 전달
        /// </summary>
        /// <param name="type"></param>
        /// <param name="msg"></param>
        public async void SendLogToServer(string type, string msg)
        {
            if(type == "INFO")
            {
                await _client.SendAsync("LOG_INFO:" + msg);
            }
            else if(type == "ERROR")
            {
                await _client.SendAsync("LOG_ERROR:" + msg);
            }
        }

        /// <summary>
        /// 이전 메시지 기록들 요청
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        public async Task RequestChatHistory(string roomId, int idNum)
        { 
            await _client.SendAsync("GET_MESSAGES:" + roomId + "," + idNum);
        }
        
        /// <summary>
        /// Room 생성
        /// </summary>
        /// <param name="room"></param>
        /// <returns></returns>
        public async Task SendRoomToServer(ChatRoom room)
        {
            string json = JsonConvert.SerializeObject(room);
            await _client.SendAsync("CREATE_ROOM:" + json);
        }

        /// <summary>
        /// 방 나가기
        /// </summary>
        /// <param name="room"></param>
        /// <returns></returns>
        public async Task SendDeleteRoomToServer(ChatRoom room)
        {
            string json = JsonConvert.SerializeObject(room);
            await _client.SendAsync("DELETE_ROOM:" + json);
        }


        /// <summary>
        /// SearchTextBox 값에 따라 UserList 변경
        /// </summary>
        private void FilterUsers()
        {
            //_allUsersBackup = AllUsers;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                AllUsers = new ObservableCollection<UserInfo>(_allUsersBackup);
            }
            else
            {
                var filtered = _allUsersBackup.Where(u => !string.IsNullOrEmpty(u.Nickname) && u.Nickname.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                //IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) -> string.Contain 과 동일한 효과에 >= 0이면 true 반환
                AllUsers = new ObservableCollection<UserInfo>(filtered);
            }
        }

        /// <summary>
        /// client 연결 종료
        /// </summary>
        /// <returns></returns>
        public void Disconnection()
        {
            _client.Disconnection();
        }
        #endregion

    }
}
