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

        // Internal States
        private int _SpaceAvailable;

        public Memory(int moduleSize, int numModules, int latency)
        {
            _theMemory = new byte[numModules, moduleSize];
            _SpaceAvailable = numModules * moduleSize;
            _latecy = latency;
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
