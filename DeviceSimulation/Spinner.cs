using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceSimulation
{
    public class Spinner
    {

        static string[] sequence = null;

        public int Delay { get; set; } = 300;


        int counter;

        public Spinner()
        {
            counter = 0;
            sequence = new string[] {
             "\\", "|", "/", "-"
        };

        }


        public void Turn(string displayMsg = "")
        {
            counter++;

            Thread.Sleep(Delay);

            int counterValue = counter % 4;

            string fullMessage = displayMsg + sequence[counterValue];
            int msglength = fullMessage.Length;

            Console.Write(fullMessage);

            Console.SetCursorPosition(Console.CursorLeft - msglength, Console.CursorTop);
        }
    }
}
