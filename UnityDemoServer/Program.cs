using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;


namespace UnityDemo_2023_7_Server
{
    public class PlayerInfo
    {
        public String PlayerName { get; set; }
        public float x, y, z;

        public override string ToString()
        {
            string str = ($"{PlayerName}   pos: {x}, {y}, {z} ");
            return str;
        }
    }

    public class Message
    {
        public string type;
        public string info;

        public Message(string type, string info)
        {
            this.type = type;
            this.info = info;
        }

        public override string ToString()
        {
            string str = ($"{this.type}, {this.info} ");
            return str;
        }
    }


    class Program
    {
        private static List<Socket> userOnline = new List<Socket>();

        private static readonly object toDoListLock = new object();
        private static List<PlayerInfo> playerInfos = new List<PlayerInfo>();
        private static Queue<Message> todoList = new Queue<Message>();


        public static void Main(string[] args)
        {
            Thread waitClientConnect = new Thread(new ThreadStart(waitClient));
            waitClientConnect.Start();

            Thread sendMessageToClient = new Thread(new ThreadStart(AutoSendAllMessageToClient));
            sendMessageToClient.Start();

            while (true)
            {
                if (todoList.Count > 0)
                {
                    Message message;

                    bool flag = false;

                    lock (toDoListLock)
                    {
                        message = todoList.Dequeue();
                        flag = true;
                    }

                    if (flag)
                    {
                        HandleMessage(message);
                        flag = false;
                    }
                }
            }
        }

        /// <summary>
        /// 处理收到的信息
        /// </summary>
        /// <param name="message"></param>
        private static void HandleMessage(Message message)
        {
            Console.WriteLine($"Message:  {message}");
            switch (message.type)
            {
                case "UpdatePlayerInfo":
                    PlayerInfo playerInfo = JsonConvert.DeserializeObject<PlayerInfo>(message.info);
                    UpdatePlayerInfo(playerInfo);
                    Console.WriteLine((object)playerInfo);
                    break;

                case "UpdateAllPlayerInfo":
                    foreach (var client in userOnline)
                    {
                        SendMessage(client, new Message("AllPlayerInfo", JsonConvert.SerializeObject(playerInfos)));
                    }

                    break;
            }
        }


        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="playerInfo"></param>
        private static void UpdatePlayerInfo(PlayerInfo playerInfo)
        {
            for (int i = 0; i < playerInfos.Count; i++)
            {
                if (playerInfos[i].PlayerName == playerInfo.PlayerName)
                {
                    playerInfos[i] = playerInfo;
                    return;
                }
            }

            //新的playerInfo
            playerInfos.Add(playerInfo);
        }

        /// <summary>
        /// 等待用户发起连接
        /// </summary>
        static void waitClient()
        {
            IPEndPoint pos = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888);

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(pos);
            //最大连接数
            socket.Listen(10);
            while (true)
            {
                Socket sClient = socket.Accept();
                userOnline.Add(sClient);
                Console.WriteLine("New Client Connected");
                Thread listenThread = new Thread(new ParameterizedThreadStart(listenClient));
                listenThread.Start(sClient);
            }
        }

        /// <summary>
        /// 监听客户端
        /// </summary>
        /// <param name="obj"></param>
        static void listenClient(object obj)
        {
            Socket socket = (Socket)obj;
            byte[] read = new byte[1024];
            Message message;
            PlayerInfo playerInfo;
            string str;
            while (true)
            {
                int len = socket.Receive(read);
                str = Encoding.Default.GetString(read, 0, len);
                foreach (string s in str.Split('&'))
                {
                    if (s.Length > 0)
                    {
                        message = JsonConvert.DeserializeObject<Message>(s);

                        lock (toDoListLock)
                        {
                            Console.WriteLine($"Enqueue Message: {message}");
                            todoList.Enqueue(message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 每隔一段时间将用户信息分发给所有客户端
        /// </summary>
        static void AutoSendAllMessageToClient()
        {
            while (true)
            {
                if (userOnline.Count > 0)
                {
                    lock (toDoListLock)
                    {
                        Message message = new Message("UpdateAllPlayerInfo", "");
                        todoList.Enqueue(message);
                        Console.WriteLine($"Enqueue Message: {message}");
                    }
                }

                Thread.Sleep(50);
            }
        }

        static void SendMessage(Socket client, Message message)
        {
            string str = JsonConvert.SerializeObject(message);
            byte[] bytes = Encoding.Default.GetBytes(str + "&");
            client.Send(bytes);
            Console.WriteLine($"Send: {str}");
        }
    }
}