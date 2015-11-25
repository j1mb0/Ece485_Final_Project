using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECE485_SatHub
{
    
    class Event
    {   
        // time
        private int _tClockStart;
        // operation
        private string _operation;
        //ts
        private int _transactionSize; 
        // tr_data_tag
        private int _trDataTags;

        public Event(int time, string operation, int tranSize, int trDataTag)
        {
            _tClockStart = time;
            _operation = operation;
            _transactionSize = tranSize;
            _trDataTags = trDataTag;
        }
    }
}
