using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WpfChatApp.Servieces;

namespace WpfChatApp.Model
{
    public enum MessageType
    {
        Text,
        Image,
        Video,
        Document,
        DateSeparator,
        OutChk
    }

    public class ChatMessage
    {
        public string RoomId { get; set; }                      // 어떤 방의 메시지인지
        public int Sender { get; set; }                         // 보낸 사람 IdNum
        public string Content { get; set; }                     // 텍스트 or 경로
        public DateTime Time { get; set; }
        public MessageType ContentType { get; set; } = MessageType.Text;
        public string FileName { get; set; }                    // 예: "test.txt"
        public string ThumbnailPath { get; set; }               // 썸네일 이미지 경로


        [JsonIgnore] public bool IsMine { get; set; }           // 내텍스트 인지 확인 하는건데 서버로는 데이터를 보내지 않음
        [JsonIgnore] public string SenderNick { get; set; }     // Sender의 닉네임 정보 받아서 저장
    }
}
