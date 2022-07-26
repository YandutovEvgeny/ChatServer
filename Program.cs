using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ChatServerAppNew;
using System.Linq;

namespace ChatServerApp
{
    class Program
    {
        //Слушает сеть, пока к нему кто-нибудь не подключится
        static TcpListener tcpListener;
        //Готовые подключения по IP адресу
        static List<TcpClient> clients;
        static UsersEntities context;
        static List<int> IdList;    //Id людей которые онлайн

        static void Main(string[] args)
        {
            context = new UsersEntities();
            List<Users> users = (from user in context.Users
                                 where user.OnLine == true
                                 select user).ToList();
            foreach(var use in users)
            {
                use.OnLine = false;
            }
            context.SaveChanges();

            tcpListener = new TcpListener(IPAddress.Any, 8888);
            clients = new List<TcpClient>();
            IdList = new List<int>();
            WaitClients();
        }

        static void WaitClients()
        {
            tcpListener.Start();
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                Console.WriteLine("К нам кто-то пришёл!");
                clients.Add(client);
                IdList.Add(-1);
                //В поток требуется передать делегат
                Thread thread = new Thread(new ParameterizedThreadStart(ListenClient));
                thread.Start(client);
            }
        }

        static void ChangedStatus(int id, bool status)
        {
            UsersEntities context = new UsersEntities();

            Users users = (from user in context.Users
                           where user.Id == id
                           select user).FirstOrDefault();
            users.OnLine = status;

            context.SaveChanges();
        }

        static void SendOnLineList()
        {
            string message = "<ONLINE> ";
            foreach(int id in IdList)
            {
                message += id.ToString() + " ";
            }
            foreach (TcpClient c in clients)
            {
                SendMessage(c, message);
            }
        }

        static void ListenClient(object client)
        {
            int id = 0;
            TcpClient tcpClient = (TcpClient)client;
            //Сетевой поток данных между клиентом и сервером
            NetworkStream networkStream = tcpClient.GetStream();
            while (true)
            {
                try
                {
                    //StreamReader читает текстовую информацию
                    StreamReader reader = new StreamReader(networkStream);
                    string message = reader.ReadLine();
                    if (message != "" && message != null)
                    {
                        if (message.IndexOf("<ID>") == 0 && id == 0)
                        {
                            id = Convert.ToInt32(message.Remove(0, 4));
                            IdList[clients.FindIndex(x => x == tcpClient)] = id;
                            ChangedStatus(id, true);
                            SendOnLineList();
                        }
                        else
                        {
                            foreach (TcpClient c in clients)
                            {
                                SendMessage(c, message);
                            }
                        }
                        Console.WriteLine(message);
                    }
                }
                catch
                {
                    ChangedStatus(id, false);
                    IdList.RemoveAt(clients.FindIndex(x => x == tcpClient));
                    clients.Remove(tcpClient);
                    SendOnLineList();
                    Console.WriteLine(id + " Покинул чат!");
                    break;
                }
            }
        }

        static void SendMessage(TcpClient tcpClient, string message)
        {
            NetworkStream networkStream = tcpClient.GetStream();
            StreamWriter streamWriter = new StreamWriter(networkStream);
            streamWriter.WriteLine(message + "\n");
            streamWriter.Flush();
        }
    }
}
