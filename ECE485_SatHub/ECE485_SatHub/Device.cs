using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECE485_SatHub
{

    class Device
    {
        const int TCK_IN_NS = 10;

        // Is an id member even necessary since the array position satisfies this requirement?
        // Keep it for now, but if we never use it tear it out.
        // TODO
        private int _id;
        private TransferRates _transferRate;
        public int linkOccupiedBy;


        // Device Constructor. Takes in the id number and transfer rate
        public Device(int id, TransferRates transferRate)
        {
            _id = id;
            _transferRate = transferRate;
            linkOccupiedBy = -1;
        }

        public ulong CalculateLatency(int ts)
        {
            double latency = (ts * 8); 
            latency /= (double)_transferRate;
            latency *= Math.Pow(10, 8);
            return Convert.ToUInt64(latency)  + 1;
        }
        
    }
}
