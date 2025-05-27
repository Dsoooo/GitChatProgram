using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfChatApp.Servieces
{
    /// <summary>
    /// 채팅방 날짜 표시선을 위한 Class
    /// </summary>
    public class DateSeparator
    {
        public DateTime Date { get; set; }
        public string DisplayDate => Date.ToString("yyyy년 M월 d일");
    }
}
