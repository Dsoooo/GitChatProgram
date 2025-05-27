using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfChatApp.Model
{
    public class ChatRoom
    {
        [JsonIgnore]
        public List<int> Participants => RoomId.Split('_').Select(int.Parse).ToList(); // 방 생성을 위해 참여자 리스트 생성
        private string _lastMessage;
        public int UserIdNum { get; set; }                    // 사용자 식별 번호 = UserInfo.IdNum
        public string RoomId { get; set; }                  // 방 고유 ID
        public string RoomName { get; set; }                // 그룹 이름 (두명 일때는 다른 사람 이름)
        public string LastMessage
        {
            get => _lastMessage;
            set
            {
                _lastMessage = value;
                //OnPropertyChanged(); // 꼭 있어야 UI가 바뀝니다
            }
        }
        public DateTime LastMessageDate { get; set; }
        public int UnReadCount { get; set; }

        public string LastMessageTimeText => LastMessageDate.ToString("yyyy-MM-dd HH:mm");


        public ObservableCollection<ChatMessage> Messages { get; set; }

        public ChatRoom(string roomId, string roomName, int userIdNum, string lastMessage, int unReadCount)
        {
            RoomId = roomId;
            RoomName = roomName;
            UserIdNum = userIdNum;
            LastMessage = lastMessage;
            Messages = new ObservableCollection<ChatMessage>();
            UnReadCount = unReadCount;
        }

        public static string CreateRoomId(List<int> participants)
        {
            var sorted = participants.OrderBy(IdNum => IdNum);
            return string.Join("_", sorted);
        }

        //public bool IsOneToOne => Participants?.Count == 2;
    }
}
