using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECE485_SatHub
{
    class Memory
    {
        // Parameters
        private int _sizeOfModule;
        private int _numberOfModules;
        private int _latecy;

        // Double array for the memory modules. 
        // One is for the memory, the second is for module number.
        private byte[,] _theMemory;

        // Internal Data Members
        private int _spaceAvailable;
        private Commands _curCmd;
        // model the buffers for in and out data
        Buffer _bufferIn;
        Buffer _bufferOut;

        public Memory(int moduleSize, int numModules, int latency)
        {
            _theMemory = new byte[numModules, moduleSize];
            _spaceAvailable = numModules * moduleSize;
            _latecy = latency;
            _curCmd = Commands.WAIT;
        }

        // TODO
        // 
        public Buffer Reap(ulong tCurClk)
        {
            // Placeholder
            _bufferOut.SetData("FF");
            return _bufferOut; 

            // check each the current transaction to see if its latency period is up.

            // if so, return data
            
            // if not, return "nothing"


        }

        public bool ParseData(Buffer data)
        {
            bool success = false;

            // Check if the data contains a command
            Commands cmd = (Commands)data._data[0];
            // If it does, we will need check the second byte for details
            // We only care about it when it is a read or a write command
            // so we can go ahead and assume the second byte will be this.
            byte tag = data._data[1];

            //if(Enum.IsDefined(typeof(Commands), cmd))
            //{
            //    switch (cmd)
            //    {
            //        case Commands.REQUEST:
            //            // read cmd
            //            // check if we have the tag. 
            //            // start getting data from memory
            //            // equivalent to read
            //            _curCmd = cmd;
            //            // TODO
            //            break;
            //        case Commands.SEND:
            //            // start putting data into memory
            //            // equivalent to write
            //            _curCmd = cmd;
            //            // TODO
            //            break;
            //        case Commands.WAIT:
            //            // we are waiting for a data transfer to complete
            //            if(_curCmd == Commands.SEND)
            //            {
            //                // Whatever?
            //            }
            //            else if(_curCmd == Commands.REQUEST)
            //            {
            //                // C
            //            }
            //            // or we need to write out memory.

            //            // TODO
            //            break;
            //        case default:
            //            // This should only be hit when the command is
            //            // invalid or jobdone, neither of which we care about
            //            break;
            //    }
            //}

            
            

            return success;

        }

        // Allocates space in the memory module for the
        // a packet with a tag. Not sure if we need this. 
        // I guess it depends on how we want to manage the memory.
        // TODO
        public bool Allocate(int tag, PacketSizes dataSize)
        {
            // I am not actually sure about this...
            return false;
        }

        // Put will attempt to insert a couple bytes into the memory.
        // TODO
        public bool AtomicPut(Buffer dataIn, int tag)
        {
            return false;
        }

        // Ask the memory if it has the data associated with a tag. 
        // TODO
        public bool QueryTag(int tag, PacketSizes dataSize)
        {
            return false;
        }

        // Get will attempt to retrieve a couple of bytes from the memory. 
        // TODO
        public bool AtomicGet(Buffer dataOut, int tag)
        {
            return false;
        }
    }
}
