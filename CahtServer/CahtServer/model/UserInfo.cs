using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfChatApp.Model
{
    public class UserInfo
    {
        public int IdNum { get; set; }          //회원 번호
        public string Name { get; set; }        
        public string Id { get; set; }
        public string Password { get; set; }
        public string Nickname { get; set; }
    }
}
