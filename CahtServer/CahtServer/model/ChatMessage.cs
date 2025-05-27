using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
        public string RoomId { get; set; }              // 어떤 방의 메시지인지
        public int Sender { get; set; }                 // 보낸 사람 IdNum
        public string Content { get; set; }             // 텍스트
        public DateTime Time { get; set; }
        public MessageType ContentType { get; set; } = MessageType.Text;
        public string FileName { get; set; }
        public string ThumbnailPath { get; set; }          
    }

}
