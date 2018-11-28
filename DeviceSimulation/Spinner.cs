using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceSimulation
{
    public class Spinner : IDisposable
    {

        private const string Sequence = @"/-\|";
        private int counter = 0;
        private readonly int delay;
        private bool active;
        private readonly Thread thread;
        private string message;

        public Spinner(int delay = 100)
        {
            this.delay = delay;
            thread = new Thread(Spin);
        }

        public void Start()
        {
            Console.Write(message);
            active = true;
            if (!thread.IsAlive)
                thread.Start();
        }

        public void Stop()
        {
            active = false;
            Draw(' ');
        }

        private void Spin()
        {
            while (active)
            {
                Turn();
                Thread.Sleep(delay);
            }
        }

        private void Draw(char c)
        {
            Console.Write(c);
            Console.SetCursorPosition(Console.CursorLeft-1, Console.CursorTop);
            
        }

        private void Turn()
        {
            
            Draw(Sequence[++counter % Sequence.Length]);
        }

        public void Dispose()
        {
            Stop();
        }

        public void setMessage(string message)
        {
            this.message = message;
        }
    }
}
