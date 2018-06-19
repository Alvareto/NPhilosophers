using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeWrapper;

/*
 * RICART i AGRAWALA protokol
 * ZAHTJEV(i, T(i))
 * ODGOVOR(j, T(i))
 */

namespace Message
{
    class Program
    {

        static void Main(string[] args)
        {
            var is_parent = !args.Any();

            // if is parent, create children
            if (is_parent)
            {
                //List<Process> ChildProcesses = new List<Process>();
                Console.WriteLine("Enter N: ");
                var N = Convert.ToInt32(Console.ReadLine());

                Console.WriteLine("Start child processes");

                var FileName = "Philosopher.exe";

                for (int i = 0; i < N; i++)
                {
                    var arg = i + " " + N;
                    Task.Run(() =>
                        Process.Start(FileName, arg)?.WaitForExit()
                    ); //.Start();
                }

                Task.WaitAll();
            }
            else
            {
                var i = Convert.ToInt32(args[0]);
                var N = Convert.ToInt32(args[0]);

                var p = new Philosopher(i, N);

            }

        }
    }

    public class Message
    {
        public enum Type
        {
            Request,
            Response
        }

        public Type type;
        public int process;
        public int clock;

        public Message(Type t = Type.Request, int p = -1, int c = -1)
        {
            this.type = t;
            this.process = p;
            this.clock = c;
        }

        public override string ToString()
        {
            return Enum.GetName(typeof(Type), this.type) + "(" + this.process + ", " + this.clock + ")";
        }
    }

    public class Philosopher
    {
        public const string PIPE_PREFIX = "Philosopher_";

        public NamedPipeServer<Message> moj_cjevovod; // tu primam poruke
        public List<NamedPipeClient<Message>> cjevovodi_ostalih_procesa = new List<NamedPipeClient<Message>>(); // tu šaljem poruke

        public Dictionary<int, NamedPipeClient<Message>> PID_to_pipeline = new Dictionary<int, NamedPipeClient<Message>>();

        public List<Message> RedPristiglihZahtjeva = new List<Message>(); // kojima trebamo poslati odgovor kad izađemo iz KO
        public List<Message> PrimljeniOdgovori = new List<Message>();
        public int responses_count => PrimljeniOdgovori.Count;

        public readonly int PID; //=> Process.GetCurrentProcess().Id;
        public readonly int TotalNumberOfProcesses;

        private string process_name => "Process " + PID + " (C: " + CLOCK + "): ";
        private int CLOCK { get; set; }

        private bool can_enter_ko => responses_count == TotalNumberOfProcesses - 1; // N - 1 responses
        private bool currently_in_ko { get; set; }

        public Philosopher(int process_number, int count)
        {
            TotalNumberOfProcesses = count;
            PID = process_number;

            open_pipes(PID);

            SendRequest();
        }

        private void open_pipes(int p)
        {
            for (int i = 0; i < TotalNumberOfProcesses; i++)
            {
                if (i != p)
                {
                    var pipe = new NamedPipeClient<Message>(PIPE_PREFIX + i);

                    cjevovodi_ostalih_procesa.Add(pipe);
                    PID_to_pipeline.Add(PID, pipe);
                }
            }

            moj_cjevovod = new NamedPipeServer<Message>(PIPE_PREFIX + p);
            moj_cjevovod.ClientMessage += ReceiveRequestResponseMessage;
        }

        private void ReceiveRequestResponseMessage(NamedPipeConnection<Message, Message> connection, Message message)
        {
            Console.WriteLine(process_name + "received message [" + message.ToString() + "]");

            CLOCK = Math.Max(CLOCK, message.clock) + 1; // increase clock

            switch (message.type)
            {
                case Message.Type.Request:
                    ProcessRequest(message);
                    break;
                case Message.Type.Response:
                    ProcessResponse(message);
                    break;
            }
        }

        private void ProcessRequest(Message msg)
        {
            RedPristiglihZahtjeva.Add(msg);
            RedPristiglihZahtjeva = RedPristiglihZahtjeva.OrderBy(o => o.clock).ToList();

            var p = RedPristiglihZahtjeva.First().process; // since we just added one, we know there is gonna be at least one

            if (!currently_in_ko && PID != p) // if P not in KO and P not top request, ...
            {
                // ... , we can send a response to request from msg.process
                SendResponse(msg);

                RedPristiglihZahtjeva.Remove(msg);
                RedPristiglihZahtjeva = RedPristiglihZahtjeva.OrderBy(o => o.clock).ToList();
            }
        }

        private void ProcessResponse(Message msg)
        {
            PrimljeniOdgovori.Add(msg);

            // now we can check should it go into critical section
            if (can_enter_ko) // all responses received
            {
                CriticalSection();
            }
        }

        // ODGOVOR(j, T(i))
        private void SendResponse(Message msg)
        {
            if (PID_to_pipeline.ContainsKey(msg.process))
            {
                PID_to_pipeline[msg.process].PushMessage(
                    new Message(t: Message.Type.Response, p: PID, c: msg.clock)
                );
            }
        }

        // ZAHTJEV(i, T(i))
        private void SendRequest() // pristupi_kriticnom_odsjecku
        {
            Console.WriteLine(process_name + "wants to enter critical section");

            var msg = new Message(t: Message.Type.Request, p: PID, c: CLOCK);
            foreach (var pipe in cjevovodi_ostalih_procesa)
            {
                pipe.PushMessage(msg);
            }
        }

        private void CriticalSection()
        {
            currently_in_ko = true;
            Console.WriteLine(process_name + " is at the table");

            ExitCriticalSection();
        }

        private void ExitCriticalSection()
        {
            PrimljeniOdgovori.Clear();
            currently_in_ko = false;

            // izlazak iz KO => pošalji odgovore na zaostale zahtjeve
            foreach (var req in RedPristiglihZahtjeva)
            {
                SendResponse(req);
            }
        }


    }
}
