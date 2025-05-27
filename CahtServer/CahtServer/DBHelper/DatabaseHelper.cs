using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using log4net;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using WpfChatApp.Model;

namespace WpfChatApp.DBHelper
{
    public class DatabaseHelper
    {
        private const string DbFile = "chatapp.db";

        private static string ConnectionString => $"Data Source={DbFile};Version=3;";
        private static readonly ILog log = LogManager.GetLogger(typeof(ChatServer));

        //데이터 베이스 동시 접근을 막기 위한 lock 코드 생성
        private static readonly object _dbLock = new();


        /// <summary>
        /// DB 시작 파일이 없는 경우 생성
        /// </summary>
        public static void Initialize()
        {
            try 
            { 
                if (!File.Exists(DbFile))
                {
                    SQLiteConnection.CreateFile(DbFile);
                }

                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();

                    using (var pragmaCmd = new SQLiteCommand("PRAGMA journal_mode=WAL;", conn))
                    {
                        pragmaCmd.ExecuteNonQuery();
                    }

                    //사용자 테이블이 없는 경우 생성
                    var cmd = new SQLiteCommand(@"
                    CREATE TABLE IF NOT EXISTS Users (
                        IdNum INTEGER PRIMARY KEY AUTOINCREMENT,
                        Id TEXT NOT NULL,
                        Password TEXT NOT NULL,
                        NickName TEXT,
                        Name TEXT
                    );", conn);
                    cmd.ExecuteNonQuery();

                    //Rooms 테이블
                    var createRoomsCmd = new SQLiteCommand(@"
                    CREATE TABLE IF NOT EXISTS Rooms (
                        RoomId TEXT,
                        UserIdNum INTEGER,
                        RoomName TEXT,                        
                        LastMessage TEXT,
                        LastMessageDate TEXT,
                        UnReadCount INTEGER DEFAULT 0,
                        PRIMARY KEY (RoomId, UserIdNum)
                    );", conn);
                    createRoomsCmd.ExecuteNonQuery();

                    //Message 테이블 생성
                    var createMessagesCmd = new SQLiteCommand(@"
                    CREATE TABLE IF NOT EXISTS Messages (
                        Idx INTEGER PRIMARY KEY AUTOINCREMENT,
                        RoomId TEXT NOT NULL,
                        Sender INTEGER NOT NULL,
                        Content TEXT NOT NULL,
                        Time TEXT NOT NULL,
                        ContentType INTEGER NOT NULL,
                        FileName TEXT,
                        ThumbnailPath TEXT
                    );", conn);
                    createMessagesCmd.ExecuteNonQuery();
                    log.Info("DataBase Start");
                }
            }
            catch(Exception ex)
            {
                log.Error("Table Create Error : " + ex.Message);
            }
        }

        /// <summary>
        /// 아이디와 비밀번호가 올바른지 체크 후 맞으면 회원정보 반환
        /// </summary>
        /// <param name="id"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static UserInfo ValidateUser(string id, string password)
        {
            lock (_dbLock)
            {
                try
                {
                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        string nickName = string.Empty;
                        conn.Open();
                        var cmd = new SQLiteCommand("SELECT * FROM Users WHERE Id = @id AND Password = @pw", conn);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@pw", password);

                        var resultStr = cmd.ExecuteReader();

                        if (resultStr.Read())
                        {
                            return new UserInfo
                            {
                                IdNum = resultStr.GetInt32(0),
                                Id = resultStr["Id"].ToString(),
                                Nickname = resultStr["Nickname"].ToString(),
                                Name = resultStr["Name"].ToString(),
                                Password = resultStr["Password"].ToString()
                            };
                        }
                        conn.Close();
                        //var result = Convert.ToInt32(cmd.ExecuteScalar());
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    log.Error("ValidateUser Error : " + ex.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// 아이디 중복 체크
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool UserExists(string id)
        {
            lock (_dbLock)
            {
                try
                {
                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();
                        using var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Users WHERE Id = @id", conn);
                        cmd.Parameters.AddWithValue("@id", id);
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 회원 가입
        /// </summary>
        /// <param name="user"></param>
        public static void AddUser(UserInfo user)
        {
            lock (_dbLock)
            {
                try
                {
                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();
                        using var transaction = conn.BeginTransaction();
                        using var cmd = new SQLiteCommand("INSERT INTO Users (Id, Nickname, Name, Password) VALUES (@id, @nickname, @name, @password)", conn);
                        cmd.Parameters.AddWithValue("@id", user.Id);
                        cmd.Parameters.AddWithValue("@nickname", user.Nickname);
                        cmd.Parameters.AddWithValue("@name", user.Name);
                        cmd.Parameters.AddWithValue("@password", user.Password);
                        cmd.ExecuteNonQuery();
                        transaction.Commit();
                        log.Info($"Create Account : [id]");
                        conn.Close();
                    }
                }
                catch (Exception ex)
                {
                    log.Error("회원 가입 에러 : " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 회원 목록 리스트 호출
        /// </summary>
        /// <returns></returns>
        public static List<UserInfo> GetAllUsers()
        {
            lock (_dbLock)
            {
                try
                {
                    var list = new List<UserInfo>();
                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();

                        var cmd = new SQLiteCommand("SELECT IdNum, Nickname FROM Users ORDER BY NickName", conn);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            list.Add(new UserInfo
                            {
                                IdNum = (reader["IdNum"] != DBNull.Value && int.TryParse(reader["IdNum"].ToString(), out int result)) ? result : 0,
                                Nickname = reader["NickName"].ToString()
                                //Name = reader["Name"].ToString()
                            });
                        }

                        conn.Close();
                        return list;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// 채팅방이 만들어질때 ROOM INSERT
        /// IGNORE을 써줌으로서 Primary Key가 중복인 경우 무시 -> 무시된 경우에도 0 값 반환
        /// </summary>
        /// <param name="room"></param>
        public static void AddRoom(ChatRoom room)
        {
            lock (_dbLock)
            {
                try
                {

                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();
                                                
                        using var transaction = conn.BeginTransaction();

                        string roomName = room.RoomName;

                        foreach (var userId in room.Participants)
                        {
                            SQLiteCommand cmd = null;
                            if (room.Participants.Count == 2)
                            {
                                cmd = new SQLiteCommand("INSERT OR IGNORE INTO Rooms (RoomId, UserIdNum, RoomName, LastMessage, LastMessageDate) SELECT @roomId, @idNum, u.Nickname, @last, @date " +
                                    "FROM Users u WHERE u.IdNum = CASE WHEN CAST(substr(@roomId, 1, instr(@roomId, '_') - 1) AS INTEGER) = @idNum" +
                                    " THEN CAST(substr(@roomId, instr(@roomId, '_') + 1) AS INTEGER)" +
                                    "     ELSE CAST(substr(@roomId, 1, instr(@roomId, '_') - 1) AS INTEGER) END", conn);
                            }
                            else
                            {
                                cmd = new SQLiteCommand("INSERT OR IGNORE INTO Rooms (RoomId, UserIdNum, RoomName, LastMessage, LastMessageDate) VALUES (@roomId, @idNum, @name, @last, @date)", conn);
                            }
                            cmd.Parameters.AddWithValue("@roomId", room.RoomId);
                            cmd.Parameters.AddWithValue("@idNum", userId);
                            cmd.Parameters.AddWithValue("@name", roomName);
                            //cmd.Parameters.AddWithValue("@participants", string.Join(",", room.Participants)); // 쉼표 구분 문자열
                            cmd.Parameters.AddWithValue("@last", room.LastMessage ?? string.Empty);
                            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                            cmd.ExecuteNonQuery();
                        }
                        
                        transaction.Commit();
                        conn.Close();
                    }
                }
                catch(Exception ex)
                {
                    log.Error("Create Room Error : " + ex.Message);
                }

            }
        }

        /// <summary>
        /// 방 나가기를 했을 경우 채팅방 메시지 기록과 방 삭제
        /// deleteChk = true 인 경우 그 유저만 단톡방을 나갔다고 보고 반환
        /// </summary>
        /// <param name="room"></param>
        public static bool DeleteRoom(ChatRoom room)
        {
            lock (_dbLock)
            {
                bool deleteChk = false;
                try
                {

                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();
                        using var transaction = conn.BeginTransaction();

                        //방에 참여 인원 확인
                        var cmd = new SQLiteCommand("SELECT count(*) CNT FROM Rooms WHERE RoomId = @id", conn);
                        cmd.Parameters.AddWithValue("@id", room.RoomId);
                        using var reader = cmd.ExecuteReader();
                        int roomCnt = 0;
                        if(reader.Read())
                        {
                            roomCnt = Convert.ToInt32(reader["CNT"]);

                        }

                        //방에 사람이 1대1인 경우 바로 삭제 단톡인 경우 방나기 처리
                        if(roomCnt == 2)
                        {
                            //메시지들 삭제
                            using (var deleteMessagesCmd = new SQLiteCommand("DELETE FROM Messages WHERE RoomId = @id", conn))
                            {
                                deleteMessagesCmd.Parameters.AddWithValue("@id", room.RoomId);
                                deleteMessagesCmd.ExecuteNonQuery();
                            }

                            // 채팅방 전체 삭제
                            using (var deleteRoomCmd = new SQLiteCommand("DELETE FROM Rooms WHERE RoomId = @id", conn))
                            {
                                deleteRoomCmd.Parameters.AddWithValue("@id", room.RoomId);
                                deleteRoomCmd.Parameters.AddWithValue("@UserIdNum", room.UserIdNum);
                                deleteRoomCmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // 채팅방 해당 인원만 삭제
                            using (var deleteRoomCmd = new SQLiteCommand("DELETE FROM Rooms WHERE RoomId = @id AND UserIdNum = @UserIdNum", conn))
                            {
                                deleteRoomCmd.Parameters.AddWithValue("@id", room.RoomId);
                                deleteRoomCmd.Parameters.AddWithValue("@UserIdNum", room.UserIdNum);
                                deleteRoomCmd.ExecuteNonQuery();

                                deleteChk = true;
                            }
                        }
                            
                        transaction.Commit();
                        conn.Close();

                        return deleteChk;
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Delete Room Error : " + ex.Message);
                    return deleteChk;
                }
            }
            
        }

        public static ChatMessage RoomOutMessageAdd(ChatRoom room, string clientId)
        {
            try
            {
                ChatMessage message = new ChatMessage
                {
                    RoomId = room.RoomId,
                    Sender = Convert.ToInt32(room.UserIdNum),
                    Content = clientId + "님이 채팅방을 나갔습니다.",
                    Time = DateTime.Now,
                    ContentType = MessageType.OutChk,
                    FileName = "",
                    ThumbnailPath = ""
                };

                SaveMessage(message, room.UserIdNum);
                log.Info(message.Content);
                return message;
            }
            catch(Exception ex)
            {
                log.Error("Room Out Message Error : " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 사용자가 포함된 채팅 목록 검색
        /// </summary>
        /// <param name="idNum"></param>
        /// <returns></returns>
        public static List<ChatRoom> GetRooms(int idNum)
        {
            lock (_dbLock)
            {
                var list = new List<ChatRoom>();
                try
                {
                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();

                        var cmd = new SQLiteCommand("SELECT * FROM Rooms WHERE UserIdNum = @idNum Order by LastMessageDate DESC", conn);
                        cmd.Parameters.AddWithValue("@idNum", idNum); 
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            list.Add(new ChatRoom(
                                reader["RoomId"].ToString(),
                                reader["RoomName"].ToString(),
                                Convert.ToInt32(reader["UserIdNum"]),
                                reader["LastMessage"].ToString(),
                                Convert.ToInt32(reader["UnReadCount"])
                            ));
                        }
                        conn.Close();
                    }
                    return list;
                }
                catch(Exception ex)
                {
                    log.Error("GetRooms Error : " + ex.Message);
                    return list;
                }
            }
        }

        /// <summary>
        /// 룸ID로 부터 룸정보 Select
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        public static (ChatRoom Room, List<int> UserIdList) GetRoomById(string roomId)
        {
            lock (_dbLock)
            {
                using var conn = new SQLiteConnection(ConnectionString);
                conn.Open();
                var cmd = new SQLiteCommand("SELECT * FROM Rooms WHERE RoomId = @roomId", conn);
                cmd.Parameters.AddWithValue("@roomId", roomId);

                ChatRoom room = null;
                var userIdList = new List<int>();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (room == null)
                    {
                        room = new ChatRoom(
                            reader["RoomId"].ToString(),
                            reader["RoomName"].ToString(),
                            Convert.ToInt32(reader["UserIdNum"]),
                            reader["LastMessage"].ToString(),
                            Convert.ToInt32(reader["UnReadCount"])
                        );
                    }

                    // 모든 유저 ID 추가
                    userIdList.Add(Convert.ToInt32(reader["UserIdNum"]));
                }

                return (room, userIdList);
            }
        }

        /// <summary>
        /// 클라이언트로부터 전송된 메시지들을 저장
        /// </summary>
        /// <param name="message"></param>
        public static void SaveMessage(ChatMessage message, int IdNum)
        {
            lock (_dbLock)
            {
                try
                {
                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();
                        using var cmd = new SQLiteCommand(@"
                        INSERT INTO Messages (RoomId, Sender, Content, Time, ContentType, FileName, ThumbnailPath)
                        VALUES (@roomId, @sender, @content, @time, @type, @fileName, @path)", conn);
                        cmd.Parameters.AddWithValue("@roomId", message.RoomId);
                        cmd.Parameters.AddWithValue("@sender", message.Sender);
                        cmd.Parameters.AddWithValue("@content", message.Content);
                        cmd.Parameters.AddWithValue("@fileName", message.FileName);
                        cmd.Parameters.AddWithValue("@time", message.Time.ToString("o")); // ISO 8601 형식
                        cmd.Parameters.AddWithValue("@type", (int)message.ContentType);
                        cmd.Parameters.AddWithValue("@path", message.ThumbnailPath);
                        cmd.ExecuteNonQuery();

                        string lastMessage = string.Empty;
                        if (message.ContentType == MessageType.Image)
                        {
                            lastMessage = "이미지";
                        }
                        else if (message.ContentType == MessageType.Document)
                        {
                            lastMessage = "문서";
                        }
                        else if (message.ContentType == MessageType.Video)
                        {
                            lastMessage = "동영상";
                        }
                        else
                            lastMessage = message.Content;

                            using var updateCmd = new SQLiteCommand(@"
                        UPDATE Rooms SET LastMessage = @last, LastMessageDate = @date, 
                                        UnReadCount = CASE
                                            WHEN UserIdNum != @idNum THEN UnReadCount + 1
                                            ELSE UnReadCount
                                        END WHERE RoomId = @roomId", conn);
                        updateCmd.Parameters.AddWithValue("@last", lastMessage);
                        updateCmd.Parameters.AddWithValue("@roomId", message.RoomId);
                        updateCmd.Parameters.AddWithValue("@idNum", IdNum);
                        updateCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        updateCmd.ExecuteNonQuery();

                        conn.Close();
                    }                  
                }
                catch(Exception ex)
                {
                    log.Error("Save Message Error : " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 방 정보에 맞는 message들 불러오는 함수
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        public static List<ChatMessage> GetMessagesByRoom(string roomId, int idNum)
        {
            lock (_dbLock)
            {
                var messages = new List<ChatMessage>();
                try
                {
                    
                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();
                        var cmd = new SQLiteCommand("SELECT * FROM Messages WHERE RoomId = @roomId ORDER BY Time", conn);
                        cmd.Parameters.AddWithValue("@roomId", roomId);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            messages.Add(new ChatMessage
                            {
                                RoomId = reader["RoomId"].ToString(),
                                Sender = Convert.ToInt32(reader["Sender"]),
                                Content = reader["Content"].ToString(),
                                Time = DateTime.Parse(reader["Time"].ToString()),
                                ContentType = (MessageType)Convert.ToInt32(reader["ContentType"]),
                                FileName = reader["FileName"].ToString(),
                                ThumbnailPath = reader["ThumbnailPath"].ToString()

                            });
                        }

                        using var resetCmd = new SQLiteCommand(@"
                            UPDATE Rooms
                            SET UnReadCount = 0
                            WHERE RoomId = @roomId and UserIdNum = @idNum", conn);

                        resetCmd.Parameters.AddWithValue("@roomId", roomId);
                        resetCmd.Parameters.AddWithValue("@idNum", idNum);
                        resetCmd.ExecuteNonQuery();
                        conn.Close();

                        return messages;
                    }
                }
                catch (Exception ex) 
                {
                    log.Error("Load Message Error : " + ex.Message);
                    return messages;
                }
            }
        }

        /// <summary>
        /// 회원 정보 업데이트, 현재는 닉네임만 가능
        /// </summary>
        /// <param name="idNum"></param>
        /// <param name="newNickname"></param>
        /// <returns></returns>
        public static bool UpdateUserInfo(int idNum, string newNickname)
        {
            lock (_dbLock)
            {
                try
                {
                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();

                        string sql = "UPDATE Users SET Nickname = @nickname WHERE IdNum = @idNum";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@nickname", newNickname);
                            cmd.Parameters.AddWithValue("@idNum", idNum);

                            int rowsAffected = cmd.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    log.Error("Update User Error : " + ex.Message);
                    return false;
                }
                  
            }
        }

    }
}
