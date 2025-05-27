using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WpfChatApp.ViewModel;

namespace WpfChatApp.Model
{
    public class ChatRoom : Notifier
    {
        [JsonIgnore]
        public ChatViewModel ViewModel { get; set; }        // ChatViewModel
        private string _lastMessage;                        // 목록에 표시할 최종 수신 메시지
        private int _unReadCount;                           // 안읽은 메시지 갯수 표기

        public int UserIdNum { get; set; }                  // 사용자 식별 번호 = UserInfo.IdNum
        public string RoomId { get; set; }                  // 방 고유 ID
        public string RoomName { get; set; }                // 그룹 이름 (두명 일때는 다른 사람 이름)
        //public List<int> Participants { get; set; }       // 참가자 ID 목록
        
        public string LastMessage
        {
            get => _lastMessage;
            set
            {
                _lastMessage = value;
                OnPropertyChanged(); 
            }
        }               
        public DateTime LastMessageDate { get; set; }
        public int UnReadCount
        {
            get => _unReadCount;
            set
            {
                if (_unReadCount != value)
                {
                    _unReadCount = value;
                    OnPropertyChanged(nameof(UnReadCount));
                }
            }
        }

        /// <summary>
        /// 메시지 최종 수신 날짜 text형으로 변환
        /// </summary>
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

        /// <summary>
        /// RoomId 만들때 참석한 사용자들 정보로 생성
        /// </summary>
        /// <param name="participants"></param>
        /// <returns></returns>
        public static string CreateRoomId(List<int> participants)
        {
            var sorted = participants.OrderBy(IdNum => IdNum);
            return string.Join("_", sorted);
        }
    }
}
