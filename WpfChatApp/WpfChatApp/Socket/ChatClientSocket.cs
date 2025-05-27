using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using log4net;
using WpfChatApp.Model;
using Newtonsoft.Json;
using System.Diagnostics;
using WpfChatApp.Servieces;


namespace WpfChatApp.Socket
{
    public class ChatClientSocket
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private StreamReader _reader;
        private Action<string> _onMessageReceived;
        //Loop 중복 생성 방지
        private bool _receivingStarted = false;

        private string _userId;
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public ChatClientSocket(Action<string> onMessageReceived)
        {
            _onMessageReceived = onMessageReceived;
            
        }

        /// <summary>
        /// 소켓 연결
        /// </summary>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        public async Task ConnectAsync(UserInfo userInfo)
        {
            try
            {
                var config = ConfigLoader.LoadConfig();

                _client = new TcpClient();
                await _client.ConnectAsync(config.ServerAddress, config.ServerPort);
                _stream = _client.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);
                await SendAsync(JsonConvert.SerializeObject(userInfo.Nickname)); //json으로 변환하여 송신

                _ = Task.Run(ReceiveLoop); // 백그라운드 수신
                log.Info("[정보] 서버 연결");
            }
            catch (Exception ex)
            {
                _onMessageReceived?.Invoke("[오류] 서버 연결 실패: " + ex.Message);
                log.Error("[오류] 서버 연결 실패: " + ex.Message);
            }
            
        }   

        /// <summary>
        /// 서버로 데이터 전송 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendAsync(string message)
        {
            try
            {
                if (_stream == null) return;
                //string json = JsonConvert.SerializeObject(message);
                //줄바꿈 표시 필요
                byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                await _stream.WriteAsync(data, 0, data.Length);
            }
            catch(Exception ex)
            {
                log.Error($"[오류] 메시지 전송 실패 : { ex.Message }");
            }
        }

        /// <summary>
        /// 서버로 부터 메시지를 수신하여 OnMessageReceived 수행
        /// </summary>
        /// <returns></returns>
        private async Task ReceiveLoop()
        {
            log.Info("Loop Start");
            try
            {
                while (true)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;
                    _onMessageReceived?.Invoke(line);
                }
            }
            catch(Exception ex)
            {
                _onMessageReceived?.Invoke("[오류] 수신 중단됨: " + ex.Message);                
            }
        }

        /// <summary>
        /// client를 재사용 하기 위한 함수
        /// </summary>
        /// <param name="client"></param>
        public void UseExistingConnection(TcpClient client)
        {
            try
            {
                _client = client;
                _stream = _client.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);

                if (!_receivingStarted)
                {
                    _receivingStarted = true;
                    _ = Task.Run(ReceiveLoop); // 백그라운드 수신 시작
                }
            }
            catch (Exception ex) { Console.WriteLine($"client.Connected: {_client.Connected}"); }

            
        }


        public void Disconnection()
        {
            try
            {
                _client?.GetStream()?.Close(); 
                _client?.Close();              
            }
            catch (Exception ex)
            {
                log.Error("Client Close Error: " + ex.Message);
            }
        }
        
    }
}
