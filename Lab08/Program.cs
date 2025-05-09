using System;
using System.Threading;
using System.IO;
using System.Runtime.Intrinsics.X86;

namespace TPProj
{
    class Program
    {
        static void Main(string[] args) => Run_estimation();

        static void Run_estimation()
        {
            const double mu = 1.0;
            const int n = 5;
            const int total_requests = 25;
            const double min_lambda = 0.2;
            const double max_lambda = 10;
            const double step_lambda = 0.2;

            for (double lambda = min_lambda; lambda <= max_lambda; lambda += step_lambda)
            {
                Run_single_estimation(Math.Round(lambda,1), mu, n, total_requests);
            }
        }

        static void Run_single_estimation(double lambda, double mu, int n, int total_requests)
        {
            Console.WriteLine($"\nlambda = {lambda}, mu = {mu}");

            var server = new Server(n, mu);
            var client = new Client(server);

            Send_requests(client, lambda, total_requests);
            Wait_server(server);

            Calculate_save(lambda, mu, n, server);
        }

        static void Send_requests(Client client, double lambda, int total_requests)
        {
            for (int id = 1; id <= total_requests; id++)
            {
                client.Send(id);
                Thread.Sleep((int)(500 / lambda));
            }
        }

        static void Wait_server(Server server)
        {
            while (server.Get_busy_channels() > 0)
            {
                Thread.Sleep(100);
            }
        }

        static void Calculate_save(double lambda, double mu, int n, Server server)
        {
            double ifo = lambda / mu;
            double P0 = Calculate_P0(ifo, n);
            double Pn = Calculate_Pn(ifo, n, P0);
            double Q = 1 - Pn;
            double A = lambda * Q;
            double k = ifo * (1 - Pn);

            double estimation_P0 = Calculate_estimation_P0(server);
            double estimation_Pn = Calculate_estimation_Pn(server);
            double estimation_Q = Calculate_estimation_Q(server);
            double estimation_A = lambda * estimation_Q;
            double estimation_K = Calculate_estimation_K(server, mu);

            Save_file(lambda, mu, P0, Pn, Q, A, k, estimation_P0, estimation_Pn, estimation_Q, estimation_A, estimation_K);
        }

        static double Calculate_estimation_P0(Server server) => (double)server.Free_time / server.Time;
        static double Calculate_estimation_Pn(Server server) => (double)server.Rejected_count / server.Request_count;
        static double Calculate_estimation_Q(Server server) => (double)server.Processed_count / server.Request_count;
        static double Calculate_estimation_K(Server server, double mu) => server.Busy_time / (server.Time * mu);
        static double Calculate_Pn(double rho, int n, double P0) => Math.Pow(rho, n) / Factorial_calculation(n) * P0;

        static void Save_file(double lambda, double mu,
                                    double P0, double Pn, double Q, double A, double k,
                                    double expP0, double expPn, double expQ, double expA, double expK)
        {
            string dataPath = Path.Combine(Environment.CurrentDirectory, "..", "data.txt");
            string dataLine = Format_data(lambda, mu, P0, Pn, Q, A, k, expP0, expPn, expQ, expA, expK);
            
            File.AppendAllText(dataPath, dataLine + Environment.NewLine);
        }

        static string Format_data(double lambda, double mu, double P0, double Pn, double Q, double A, double k,
                    double estP0, double estPn, double estQ, double estA, double estK) =>
                        string.Format("{0} {1} {2:F4} {3:F4} {4:F4} {5:F4} {6:F4} {7:F4} {8:F4} {9:F4} {10:F4} {11:F4}",
                        lambda, mu, P0, Pn, Q, A, k, estP0, estPn, estQ, estA, estK);

        static double Calculate_P0(double rho, int n)
        {
            double sum = 0;
            for (int i = 0; i <= n; i++)
            {
                sum += Math.Pow(rho, i) / Factorial_calculation(i);
            }
            return 1 / sum;
        }

        static double Factorial_calculation(int k) => k <= 1 ? 1 : k * Factorial_calculation(k - 1);
    }

    struct PoolRecord
    {
        public Thread? Thread;
        public bool InUse;
    }

    public class Server
    {
        private readonly PoolRecord[] pool;
        private readonly object stats_lock = new object();
        private readonly DateTime[] start_times;
        private readonly DateTime start_time;
        private readonly double mu;

        public int Request_count { get; private set; }
        public int Processed_count { get; private set; }
        public int Rejected_count { get; private set; }
        public double Busy_time { get; private set; }
        public double Free_time { get; private set; }
        public double Time { get; private set; }

        public Server(int poolSize, double service_rate)
        {
            pool = new PoolRecord[poolSize];
            start_times = new DateTime[poolSize];
            mu = service_rate;
            start_time = DateTime.Now;
        }

        public void Proc(object? sender, ProcEventArgs e)
        {
            if (e == null) return;

            lock (stats_lock)
            {
                Request_count++;
                Time = (DateTime.Now - start_time).TotalSeconds;
                Console.WriteLine($"Заявка c №{e.Id} поступила на сервер");

                if (Get_busy_channels() == 0)
                {
                    Free_time += (DateTime.Now - start_time).TotalSeconds - Time;
                }
                Process_reject(e.Id);
            }
        }

        private void Process_reject(int request_id)
        {
            if (Find_channel(out int available_channel))
            {
                Process_request(request_id, available_channel);
            }
            else
            {
                Rejected_count++;
                Console.WriteLine($"Заявка №{request_id} отклонена");
            }
        }

        private bool Find_channel(out int available_channel)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].InUse)
                {
                    available_channel = i;
                    return true;
                }
            }
            available_channel = -1;
            return false;
        }

        private void Process_request(int request_id, int channel_index)
        {
            pool[channel_index].InUse = true;
            start_times[channel_index] = DateTime.Now;
            pool[channel_index].Thread = new Thread(() => 
            {
                Console.WriteLine($"Начата обработка заявки №{request_id}");
                Thread.Sleep((int)(500 / mu));
                
                lock (stats_lock)
                {
                    Busy_time += (DateTime.Now - start_times[channel_index]).TotalSeconds;
                    pool[channel_index].InUse = false;
                    Console.WriteLine($"Заявка №{request_id} обработана в канале {channel_index + 1}");
                }
            });
            pool[channel_index].Thread.Start();
            Processed_count++;
            Console.WriteLine($"Заявка №{request_id} принята в канал {channel_index + 1}");
        }

        public int Get_busy_channels()
        {
            int count = 0;
            foreach (var record in pool)
            {
                if (record.InUse) count++;
            }
            return count;
        }
    }

    public class Client
    {
        public event EventHandler<ProcEventArgs> Request;

        public Client(Server server) => Request += server.Proc;

        public void Send(int id) => Request?.Invoke(this, new ProcEventArgs { Id = id });
    }

    public class ProcEventArgs : EventArgs
    {
        public int Id { get; set; }
    }
}