using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceSimulationTesting.Testing
{
    class Model
    {
        public double temperature { get; set; }
        public double humidity { get; set; }
        public string ID { get; set; }
        public DateTime date { get; set; }

        public Model(double temperature, double humidity, string id, DateTime date)
        {
            this.temperature = temperature;
            this.humidity = humidity;
            ID = id;
            this.date = date;
        }
    }
}
