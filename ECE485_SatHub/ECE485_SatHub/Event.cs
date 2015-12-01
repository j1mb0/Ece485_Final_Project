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
        public ulong _tClockStart;
        // operation
        public string _operation;
        // device id
        public int _deviceId;
        //ts
        public int _transactionSize; 
        // tr_data_tag
        public int _trDataTags;
        // time the event ended 
        public ulong _tClockEnd;
        // The id of the Event
        public int _eventId;
        
        

        public Event(ulong time, string operation, int deviceId, int tranSize, int trDataTag, int eventId)
        {
            _tClockStart = time;
            _operation = operation;
            _deviceId = deviceId;
            _transactionSize = tranSize;
            _trDataTags = trDataTag;
            _tClockEnd = 0;
            _eventId = eventId;
        }
    }
}
