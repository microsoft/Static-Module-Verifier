using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace SmvLibrary
{
    class ProcessTimer : Timer
    {
        public Process process { get; set; }
        public Double maxMemory { get; set; }

        public ProcessTimer(ref Process process, Double maxMemory, Double interval) : base(interval)
        {
            this.process = process;
            this.maxMemory = maxMemory;
        }
    }
}
