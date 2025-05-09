using System;
using System.Threading;

namespace TPProj
{
    class Program
    {
        static void Main()
        {
            Server server = new Server();
            Client client = new Client(server);

            for (int id = 1; id <= 100; id++)
            {
                client.Send(id);
                Thread.Sleep(50);
            }
            Console.WriteLine("Всего заявок: {0}", server.RequestCount);
            Console.WriteLine("Обработано заявок: {0}", server.ProcessedCount);
            Console.WriteLine("Отклонено заявок: {0}", server.RejectedCount);
        }
    }

    struct PoolRecord
    {
        public Thread Thread;
        public bool InUse;
    }

    class Server
    {
        private PoolRecord[] _pool;
        private object _threadLock = new object();
        public int RequestCount { get; private set; } = 0;
        public int ProcessedCount { get; private set; } = 0;
        public int RejectedCount { get; private set; } = 0;

        public Server()
        {
            _pool = new PoolRecord[5];
        }
        public void ProcessRequest(object sender, ProcEventArgs e)
        {
            lock (_threadLock)
            {
                Console.WriteLine("Заявка с номером: {0}", e.Id);
                RequestCount++;

                // Поиск свободного потока в пуле
                for (int i = 0; i < 5; i++)
                {
                    if (!_pool[i].InUse)
                    {
                        _pool[i].InUse = true;
                        _pool[i].Thread = new Thread(new ParameterizedThreadStart(Process));
                        _pool[i].Thread.Start(e.Id);
                        ProcessedCount++;
                        return;
                    }
                }
                RejectedCount++;
            }
        }

        public void Process(object arg)
        {
            int id = (int)arg;
            Console.WriteLine("Обработка заявки: {0}", id);
            Thread.Sleep(500); 
            for (int i = 0; i < 5; i++)
            {
                if (_pool[i].Thread == Thread.CurrentThread)
                {
                    _pool[i].InUse = false;
                    break;
                }
            }
        }
    }

    class Client
    {
        private Server _server;
        public event EventHandler<ProcEventArgs> Request;

        public Client(Server server)
        {
            _server = server;
            this.Request += _server.ProcessRequest;
        }

        public void Send(int id)
        {
            ProcEventArgs args = new ProcEventArgs();
            args.Id = id;
            OnRequest(args);
        }

        protected virtual void OnRequest(ProcEventArgs e)
        {
            Request?.Invoke(this, e);
        }
    }

    public class ProcEventArgs : EventArgs
    {
        public int Id { get; set; }
    }
}