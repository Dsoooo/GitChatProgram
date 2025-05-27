using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using WpfChatApp.Model;
using WpfChatApp.Servieces;
using WpfChatApp.Socket;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace WpfChatApp.ViewModel
{
    /// <summary>
    /// 기존 MainviewModel로만 처리 할 경우 여러 채티창을 킨 경우 메시지와 UI가 섞여서 분리
    /// </summary>
    public class ChatViewModel : Notifier
    {
        #region variable

        private string _currentMessage;
        private ChatClientSocket _client;
        private UserInfo _userInfo;
        private MainViewModel _mainViewModel;

        #endregion

        #region properties

        public string CurrentMessage
        {
            get => _currentMessage;
            set
            {
                _currentMessage = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<ChatMessage> Messages { get; set; }
        public ICommand SendMessageCommand { get; set; }
        public ICommand SendFileCommand { get; set; }
        public ChatRoom Room { get; set; }
        public string FileName { get; set; }  // 파일명

        #endregion

        #region constuctors

        /// <summary>
        /// ChatViewModel 생성자
        /// </summary>
        /// <param name="room"></param> 채티방 마다 room class를 가짐
        /// <param name="currentUser"></param> 현재 사용자
        public ChatViewModel(MainViewModel mv)
        {
            _mainViewModel = mv;
            Room = mv.SelectedRoom;
            _userInfo = mv.UserInfo;
            Messages = new ObservableCollection<ChatMessage>();

            SendMessageCommand = new RelayCommand(SendMessage, CanSendMessage);
            SendFileCommand = new RelayCommand(SendFile);
        }

        #endregion

        #region methods

        /// <summary>
        /// 내 메시지
        /// </summary>
        private async void SendMessage()
        {
            try
            {
                var message = new ChatMessage
                {
                    RoomId = Room.RoomId,
                    Sender = _userInfo.IdNum,
                    Content = CurrentMessage,
                    Time = DateTime.Now,
                    IsMine = true,
                    SenderNick = "나"
                };

                CheckDate();

                _mainViewModel.SendMessageToServer(message);

                //UI 바로 반영 추가 내용
                message.IsMine = true;
                Messages.Add(message);
                Room.LastMessage = message.Content;

                CurrentMessage = string.Empty;
            }
            catch (Exception ex) 
            {
                SendLog("ERROR", "Send Message Error : " + ex.Message); 
            }

        }

        /// <summary>
        /// log를 서버로 보냄
        /// </summary>
        /// <param name="type"></param>
        /// <param name="msg"></param>
        public void SendLog(string type, string msg)
        {
            _mainViewModel.SendLogToServer(type, msg);
        }

        /// <summary>
        /// 메시지 수신
        /// </summary>
        /// <param name="message"></param>
        public void ReceivedMessage(ChatMessage message)
        {
            message.IsMine = (message.Sender == _userInfo.IdNum);
            if (!message.IsMine)
                Messages.Add(message);
        }

        /// <summary>
        /// 채티방 이전 채팅내역 요청
        /// </summary>
        /// <returns></returns>
        public async Task LoadChatHistoryAsync()
        {
            await _mainViewModel.RequestChatHistory(Room.RoomId, Room.UserIdNum);
        }

        /// <summary>
        /// 메시지가 있을때만 보내도록 체크 하는 함수
        /// </summary>
        /// <returns></returns>
        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(CurrentMessage);
        }

        /// <summary>
        /// 파일 보내는 함수(이미지, 동영상, 문서 파일 등)
        /// HTTP 클라이언트와 연결해서 Rest API를 사용해서 파일 업로드, 다운로드
        /// </summary>
        private async void SendFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            //dialog.Filter = "Image and Video Files|*.jpg;*.jpeg;*.png;*.gif;*.mp4;*.avi";
            dialog.Filter = "모든파일 (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                var filePath = dialog.FileName;
                var extension = System.IO.Path.GetExtension(filePath).ToLower();
                var fileName = System.IO.Path.GetFileName(filePath);
                string type = string.Empty;

                MessageType messageType = MessageType.Text;

                if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif" || extension == ".bmp")
                {
                    messageType = MessageType.Image;
                    type = "이미지";
                }
                else if (extension == ".mp4" || extension == ".avi")
                {
                    messageType = MessageType.Video;
                    type = "동영상";
                }
                //else if (extension == ".xlsx" || extension == ".xls" || extension == ".csv" || extension == ".pdf" || extension == ".doc" || extension == ".docx")
                else
                {
                    messageType = MessageType.Document;
                    type = "문서";
                }

                try
                {
                    //HTTP 서버와 통신하기 위해 사용하는 클래스
                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        var form = new System.Net.Http.MultipartFormDataContent();
                        var fileContent = new System.Net.Http.ByteArrayContent(System.IO.File.ReadAllBytes(filePath));
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                        form.Add(fileContent, "file", fileName);

                        var config = ConfigLoader.LoadConfig();

                        // 서버 주소에 맞게 수정 필요
                        string serverBaseUrl = config.HttpAddress;
                        var response = await httpClient.PostAsync(config.HttpAddress + "/ChatApp/files/upload", form);
                        if (response.IsSuccessStatusCode)
                        {
                            var resultJson = await response.Content.ReadAsStringAsync();
                            dynamic result = JsonConvert.DeserializeObject(resultJson);
                            string fileUrl = serverBaseUrl + result.fileUrl;
                            string thumbnailPath = string.Empty;

                            if (messageType == MessageType.Video)
                            {
                                thumbnailPath = await CreateAndUploadThumbnailAsync(filePath, config.HttpAddress + "/ChatApp/files/upload", serverBaseUrl);
                            }              

                            var message = new ChatMessage
                            {
                                RoomId = Room.RoomId,
                                Sender = _userInfo.IdNum,
                                Content = fileUrl,           // 서버에 저장된 경로 
                                FileName = fileName,         // 선택한 파일 이름
                                Time = DateTime.Now,
                                ContentType = messageType,
                                IsMine = true,
                                ThumbnailPath = thumbnailPath,
                                SenderNick = "나"
                            };

                            message.IsMine = true;
                            _mainViewModel.SendMessageToServer(message);
                            Messages.Add(message);
                            Room.LastMessage = type;
                        }
                        else
                        {
                            MessageBox.Show("파일 업로드 실패: " + response.StatusCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("파일 전송 중 오류 발생: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 썸네일 생성 함수
        /// FFMPEG exe 활용
        /// Fast Forward MPEG의 약자로, 비디오, 스트리밍, 녹화, 인코딩, 디코딩, 썸네일 생성 등 거의 모든 멀티미디어 작업을 명령어 기반으로 처리할 수 있는 도구
        /// </summary>
        /// <param name="videoPath"></param>
        /// <param name="thumbnailOutputPath"></param>
        /// <returns></returns>
        public static async Task<string> CreateAndUploadThumbnailAsync(string videoPath, string serverUploadUrl, string serverBaseUrl)
        {
            try
            {
                if (!File.Exists(videoPath))
                {
                    MessageBox.Show("동영상 파일을 찾을 수 없습니다: " + videoPath);
                    return null;
                }

                // ffmpeg.exe 경로
                string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");
                if (!File.Exists(ffmpegPath))
                {
                    MessageBox.Show("ffmpeg.exe를 찾을 수 없습니다: " + ffmpegPath);
                    return null;
                }

                // 썸네일 파일명 구성
                string fileNameNoExt = Path.GetFileNameWithoutExtension(videoPath);
                string localThumbnailPath = Path.Combine(Path.GetTempPath(), fileNameNoExt + ".jpg");
                string serverThumbnailFileName = "thumb_" + fileNameNoExt + ".jpg";

                // FFmpeg 명령어 실행해 썸네일 생성
                string args = $"-y -ss 00:00:01 -i \"{videoPath}\" -vframes 1 \"{localThumbnailPath}\"";
                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = new Process { StartInfo = startInfo };
                process.Start();
                string stdOut = await process.StandardOutput.ReadToEndAsync();
                string stdErr = await process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode != 0 || !File.Exists(localThumbnailPath))
                {
                    MessageBox.Show("썸네일 생성 실패:\n" + stdErr);
                    return null;
                }

                // 서버로 업로드             
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var form = new System.Net.Http.MultipartFormDataContent();
                    var fileContent = new System.Net.Http.ByteArrayContent(System.IO.File.ReadAllBytes(localThumbnailPath));
                    
                    form.Add(fileContent, "file", serverThumbnailFileName);

                    var response = await httpClient.PostAsync(serverUploadUrl, form);
                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("썸네일 업로드 실패: " + response.StatusCode);
                        return null;
                    }

                    var resultJson = await response.Content.ReadAsStringAsync();
                    dynamic result = JsonConvert.DeserializeObject(resultJson);
                    string fileUrl = serverBaseUrl + result.fileUrl;
                    return fileUrl;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("썸네일 생성/업로드 오류: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 채팅창에 오늘 첫 입력인지 체크 후 날짜 구분선 추가
        /// </summary>
        public void CheckDate()
        {
            var now = DateTime.Now.Date;
            var lastMsg = Messages.LastOrDefault(m => m.ContentType != MessageType.DateSeparator);
            var lastDate = lastMsg?.Time.Date;

            // 날짜가 바뀐 경우 → 날짜 구분선 추가
            if (lastDate == null || lastDate.Value != now)
            {
                Messages.Add(new ChatMessage
                {
                    ContentType = MessageType.DateSeparator,
                    Time = DateTime.Now
                });
            }
        }

        #endregion


    }
}
