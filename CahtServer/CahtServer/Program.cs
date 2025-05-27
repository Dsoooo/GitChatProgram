using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using Newtonsoft.Json;
using WpfChatApp.DBHelper;
using WpfChatApp.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

class ClientInfo
{
    public TcpClient Client { get; set; }
    public NetworkStream Stream { get; set; }
    public StreamReader Reader { get; set; }
    public string Id { get; set; }
    public int IdNum { get; set; }
}

class ChatServer
{
    private static TcpListener listener;
    private static ConcurrentDictionary<TcpClient, ClientInfo> clients = new(); 
    private static readonly ILog log = LogManager.GetLogger(typeof(ChatServer));
 
    /// <summary>
    /// 서버 Main
    /// TCP 서버 시작 Port No : 9000
    /// </summary>
    /// <returns></returns>
    static async Task Main(string[] args)
    {
        //Log4net 초기화
        XmlConfigurator.Configure(new FileInfo("log4net.config"));

        // 웹 서버 비동기 실행
        var webServerTask = Task.Run(() => StartWebServer(args));

        // TCP 서버 수신
        listener = new TcpListener(IPAddress.Any, 9000);
        listener.Start();
        Console.WriteLine("서버가 시작되었습니다.");
        log.Info("서버 시작");

        //DB생성 및 시작
        DatabaseHelper.Initialize();

        while (true)
        {
            //새로운 client가 계속 넘겨지는지 체크
            TcpClient client = await listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client); // stream은 안 넘겨도 됨
        }

    }

    /// <summary>
    /// web 서버 시작 
    /// IP Address : 192.168.7.14
    /// Port No : 5000
    /// file 경로 : \프로젝트폴더\UploadFiles
    /// 파일을 수신받으면 Guid로 변경해서 저장
    /// HTTP 로 스트리밍
    /// </summary>
    /// <param name="args"></param>
    private static void StartWebServer(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");
        Directory.CreateDirectory(uploadPath);

        var thumbNailPath = Path.Combine(uploadPath, "Thumbnails");
        if (!Directory.Exists(thumbNailPath))
        {
            Directory.CreateDirectory(thumbNailPath);
        }

        app.MapPost("/ChatApp/files/upload", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            var file = form.Files["file"];

            if (file is null || file.Length == 0)
                return Results.BadRequest("파일 없음");

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var savePath = Path.Combine(uploadPath, fileName);

            using var stream = new FileStream(savePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var fileUrl = $"/files/{fileName}";
            return Results.Ok(new { fileName = file.FileName, fileUrl });
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadPath),
            RequestPath = "/files"
        });

        app.Run("http://192.168.7.114:5000");
        log.Info("Server Start");
    }


    /// <summary>
    /// 각 client 마다 비동기적으로 사용되는 독립 세션
    /// async로 비동기적으로 처리
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    private static async Task HandleClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
         var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
         
        string msg = await reader.ReadLineAsync();
        if (msg == null) return;

        UserInfo user = new UserInfo();

        if (msg.StartsWith("REGISTER:"))
        {
            string json = msg.Substring("REGISTER:".Length);
            user = JsonConvert.DeserializeObject<UserInfo>(json);

            //중복체크 확인한 경우에만 가능하도록 추후 수정
            if (DatabaseHelper.UserExists(user.Id))
            {
                await writer.WriteLineAsync("ID_EXISTS");
            }
            else
            {
                DatabaseHelper.AddUser(user);
                await writer.WriteLineAsync("SUCCESS");
            }

            return; // 회원가입은 여기서 종료
        }
        else if(msg.StartsWith("IDCHECK:"))
        {
            string json = msg.Substring("IDCHECK:".Length);
            string userId = JsonConvert.DeserializeObject<string>(json);

            if (DatabaseHelper.UserExists(userId))
            {
                await writer.WriteLineAsync("ID_EXISTS");
            }
            else
            {
                await writer.WriteLineAsync("ID_NOT_EXISTS");
            }

            // 아이디 중복 체크후 종료
            return;
        }
        else if(msg.StartsWith("LOGIN:"))
        {
            //string result = string.Empty;
            string json = msg.Substring("LOGIN:".Length);
            user = JsonConvert.DeserializeObject<UserInfo>(json);

            //아이디 체크
            if (!DatabaseHelper.UserExists(user.Id))
            {
                await writer.WriteLineAsync("ID_NOT_EXISTS");
                return;
            }

            //비밀번호 체크
            user = DatabaseHelper.ValidateUser(user.Id, user.Password);
            if (user != null)
            {
                //아이디와 비밀번호가 맞는 경우 닉네임 반환 후 로그인 성공
                json = JsonConvert.SerializeObject(user);
                await writer.WriteLineAsync("USER:" + json);

                //로그인 성공시 전체 사용자 리스트 반환
                var userList = DatabaseHelper.GetAllUsers(); // List<UserInfo>
                json = JsonConvert.SerializeObject(userList);
                await writer.WriteLineAsync("USERLIST:" + json);

                await Task.Delay(200);

                //로그인 성공시 사용자의 채팅 목록 반환
                var roomList = DatabaseHelper.GetRooms(user.IdNum);
                json = JsonConvert.SerializeObject(roomList);
                await writer.WriteLineAsync("ROOMLIST:" + json);
            }
            else
            {
                await writer.WriteLineAsync("PASSWORD_WRONG");
                return;
            }           
        }

        // 사용자 구분, 채팅
        //string clientId = await reader.ReadLineAsync();
        string clientId = user.Nickname;
        var info = new ClientInfo
        {
            Client = client,
            Stream = stream,
            Reader = reader,
            Id = clientId,
            IdNum = user.IdNum
        };

        clients.TryAdd(client, info);
        //Console.WriteLine($"[{clientId}] 접속");
        log.Info($"[{clientId}] 접속");

        try
        {
            while (true)
            {
                msg = await reader.ReadLineAsync();
                if (msg == null) break;

                if (msg.StartsWith("CREATE_ROOM:"))
                {
                    string json = msg.Substring("CREATE_ROOM:".Length);
                    ChatRoom newRoom = JsonConvert.DeserializeObject<ChatRoom>(json);

                    // DB에 ROOM 저장
                    DatabaseHelper.AddRoom(newRoom);

                    //await writer.WriteLineAsync("ROOM_CREATEROOM_SUCCESS");
                }
                else if (msg.StartsWith("DELETE_ROOM:"))
                {
                    string json = msg.Substring("DELETE_ROOM:".Length);
                    ChatRoom existingRoom = JsonConvert.DeserializeObject<ChatRoom>(json);

                    //삭제 명령 들어와서 True : 해당 인원만 나갔다는 메시지를 return
                    // False : 메시지, 방 전체 삭제
                    if (DatabaseHelper.DeleteRoom(existingRoom))
                    {
                        ChatMessage outMsg = DatabaseHelper.RoomOutMessageAdd(existingRoom, clientId);

                        await BroadcastAsync(outMsg);
                    }
                    else 
                    {
                        string deletRoomMsg = "DELETE_ROOM:" + JsonConvert.SerializeObject(existingRoom);

                        await writer.WriteLineAsync(deletRoomMsg);
                    }
                }
                else if(msg.StartsWith("MESSAGE:"))
                {
                    string json = msg.Substring("MESSAGE:".Length);
                    ChatMessage message = JsonConvert.DeserializeObject<ChatMessage>(json);
                    Console.WriteLine($"[{clientId}] {message.Content}");

                    DatabaseHelper.SaveMessage(message, info.IdNum);

                    await BroadcastAsync(message);
                }
                else if (msg.StartsWith("GET_MESSAGES:"))
                {
                    string str = msg.Substring("GET_MESSAGES:".Length);
                    var subStr = str.Split(',');
                    if (subStr.Length != 2) return;

                    string roomId = subStr[0];
                    int userIdNum = int.Parse(subStr[1]);
                    var messages = DatabaseHelper.GetMessagesByRoom(roomId, userIdNum);

                    await Task.Delay(200);

                    string json = "MESSAGES:" + JsonConvert.SerializeObject(messages);

                    await writer.WriteLineAsync(json);
                }
                else if(msg.StartsWith("USER_UPDATE:"))
                {
                    string json = msg.Substring("USER_UPDATE:".Length);
                    UserInfo changeUser = JsonConvert.DeserializeObject<UserInfo>(json);                    
                    if(DatabaseHelper.UpdateUserInfo(changeUser.IdNum, changeUser.Nickname))
                    {
                        log.Info($"[{clientId}] NickName Change to : {changeUser.Nickname}");
                    }
                }
                else if(msg.StartsWith("LOG_INFO:"))
                {
                    string clientStr = info.IdNum + "_" + clientId;
                    string logStr = msg.Substring("LOG_INFO:".Length);
                    log.Info(($"[{clientId}] " + logStr));
                }
                else if (msg.StartsWith("LOG_ERROR:"))
                {
                    string clientStr = info.IdNum + "_" + clientId;
                    string logStr = msg.Substring("LOG_ERROR:".Length);
                    log.Info(($"[{clientId}] " + logStr));
                }


            }
        }
        catch (Exception ex)
        {
            log.Error($"[{clientId}] 에러: {ex.Message}");
        }
        finally
        {
            clients.TryRemove(client, out _);
            client.Close();
            log.Info($"[{clientId}] 연결 종료");

        }
    }

    /// <summary>
    /// 클라이언트에서 송신한 메시지의 Chat Data에서 수신자를 찾아서 전송
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private static async Task BroadcastAsync(ChatMessage message)
    {

        string json = JsonConvert.SerializeObject(message);

        // RoomId에 해당하는 채팅방 정보를 DB 또는 메모리에서 가져옴
        var (room, userList) = DatabaseHelper.GetRoomById(message.RoomId);
        if (room == null)
        {
            log.Warn($"[Broadcast] Room not found: {message.RoomId}");
            return;
        }

        //메시지와 함께 룸정보 전송
        string roomJson = JsonConvert.SerializeObject(room);

        byte[] data = Encoding.UTF8.GetBytes("MESSAGE:" + json + "ROOM:" + roomJson + "\n");

        foreach (var kvp in clients)
        {
            var clientInfo = kvp.Value;

            // 참여자만에게 전송 (RoomId 기반 or ParticipantId 리스트 기반)
            if (message.RoomId != null &&
                userList.Contains(clientInfo.IdNum) && clientInfo.IdNum != message.Sender) 
            {
                try
                {
                    await Task.Delay(10);
                    await clientInfo.Stream.WriteAsync(data, 0, data.Length);
                }
                catch(Exception ex)
                {
                    // 실패 시 클라이언트 제거
                    log.Error("메시지 전송 실패 : " + ex.Message);
                }
            }
        }
    }


}
