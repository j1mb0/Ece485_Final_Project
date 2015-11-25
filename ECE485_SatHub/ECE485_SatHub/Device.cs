using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECE485_SatHub
{

    class Device
    {
        // Is an id member even necessary since the array position satisfies this requirement?
        // Keep it for now, but if we never use it tear it out.
        // TODO
        private int _id;
        private TransferRates _transferRate;

        // Keep track of events for scheduling simulation. 
        // THIS DOES NOT RELATE TO HARDWARE IN ANYWAY.
        private List<Event> _listOfEvents;

        // Model the buffer for the link. One for recieving, one for sending.  
        Buffer _bufferIn;
        Buffer _bufferOut;

        // Device Constructor. Takes in the id number and transfer rate
        public Device(int id, TransferRates transferRate)
        {
            _id = id;
            _transferRate = transferRate;
            _listOfEvents = new List<Event>();

        }

        // Add an event to the current device. 
        public void AddEvent(int time, string operation, int tranSize, int trDataTag)
        {
            // allocate the memory for the event and add it to the list. 
            Event eventToAdd = new Event(time, operation, tranSize, trDataTag);
            _listOfEvents.Add(eventToAdd);
        }

        // This services the device. 
        // I do not know what it returns yet. Maybe we need to
        // model the data bus via the return value?
        // Feel free to break this up as desired. 
        // TODO
        public Buffer Service(ulong tCurrentClock)
        {
            // Placeholder
            _bufferIn.SetData("FF");
            return _bufferIn;

            // Check each Event to see if any are active.

            // Choose the first valid Event
            // If there are none,
                // because all events have been completed
                    // return a job finished command.
                // or because we are waiting for a transaction to finish.
                    // return a wait for data command.

            // If it is a SEND transaction
                // Check if we need to initiate the SEND transaction
                    // return a command to write to memory and the data tag. 
                // See if we can recieve data for the Event. (buffer full)
                    // Return the data

            // If it is a REQUEST transaction
            // See if we can send data for the Event.
               // Send the data (or request it from the memory?)
               // If we need to request the data (it is not in buffer)
                    // Maybe return a command and tag for data request?

            // END

            // ASSUMPTION
            // I think that all of the above steps are based
            // off of differences from the current clock value.
            // For example, 
            // 1. Recieve a Send Command at tck = 0
            // 2. Wait for the data to be recieved in the buffer.
            //      * Calculate this based off _TransferRate. Add one for the buffer latency. 
            // 3. Return the data when the calculated number of clock cycles have passed.
            // 4. Do this until all data for the Event has been transferred. 
 
            // TODO
            // A link can send and recieve simultaneously. I think there is certainly enough
            // latency in the wireless links to use only one bus between the hub and the link.
            // This may effect how this method works, and maybe the psuedo-code above does not
            // reflect that. Need to address this. 
            // If we include two busses (one for in, one for out), then maybe
            // this has to be a ServiceIn and a ServiceOut method. 
        }

        // In case the memory is full or we need to retrieve the data from the data center,
        // we need to be able to stall a transfer.
        // TODO
        public void Stall(ulong tCurrentClock)
        {
            // Save the time the stall began. (so we can use it in the calculations in Service) 
            // This might be tricky. 
            // ASSUMPTION: We want to minimize stall time. That means we may want to recieve 
            // as many bytes as possible and stall during the middle of transfers versus
            // stalling a 1024 byte transfer because we only have 512 available. 
            // Grab the 512 and put it into memory, and every time a couple bytes free up unstall it.
            // Anyone else think this is a good idea? 
            // No? 
            // Just me?
            // Possibly my Dad?

            // Mark the device as Stall Send or Stall Receive.
            // A stall send occurs because our memory is full and we need to transfer 
            // data to the data center via the satellite before we can make room. 
            // A stall recieve occurs because we recieved a request for data that has been evicted
            // and we need to get the data from the data center via the satellite link. 
        }
    }
}
