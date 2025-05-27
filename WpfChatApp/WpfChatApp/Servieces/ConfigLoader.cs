using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfChatApp.Servieces
{
    public class ServerConfig
    {
        public string ServerAddress { get; set; }
        public int ServerPort { get; set; }
        public string HttpAddress { get; set; }
    }

    /// <summary>
    /// 서버 접속 정보를 json 파일로 부터 읽어옴
    /// </summary>
    public static class ConfigLoader
    {
        public static ServerConfig LoadConfig(string filePath = "serverconfig.json")
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("설정 파일을 찾을 수 없습니다.", filePath);

            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ServerConfig>(json);
        }
    }
}
