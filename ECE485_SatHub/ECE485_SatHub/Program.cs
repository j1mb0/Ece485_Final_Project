/**
 * ECE 485 Final Project
 * This is the file that emulates the Satellite Hub.
 * It is a console application that takes 1 argument that is the file
 * path to the .csv file holding the correctly formatted traffic information.
 * 
 * ASSUMPTIONS:
 * Here is a list of assumptions made so far.
 * 1. All data transferred will be ascii characters, 
 * therefore commands are all 1 byte (invalid ascii characters).
 * 2a. Each tag refers to a chunk of memory, and all of this is 
 * tracked using a small section of memory in M1. We refer to this 'directory'
 * when writing or reading data from memory to get/set the addresses the tags
 * are stored at. 
 * OR 
 * 2b. Each line of memory has a byte reserved for what tag is stored inside.
 * When reading from memory, the tag is put on the address bus and the
 * 3. Data will only exist in one location in memory. E.G. It cannot exist in M1 and M2
 * at the same time. This enables whichever 2 we choose. 
 * 4. Since our device links are all serial, we use buffers to hold the data during the transfer.
 * 5. Each device link (input & output?) and memory (M1-M3) has its own bus. 
 * 6. Each transaction size (ts) includes the command and tag information. 
 *          IF WE ASSUME THIS, then we do not have to add extra waits for the commands. Simplifies code. 
 * 
 * *** TODO ***
 * Add all assumptions scattered through the code here. If I included an assumption
 * in the code, I marked it near the implementation of the assumption. Search the project
 * for ASSUMPTION to find all of these. 
 * 
 * Also note that code that is a placeholder, not complete, or otherwise imperfect
 * is marked by a TODO. Searching the project for TODO will take you to all of these 
 * sections. There will always be an explaination in comments near the TODO. 
 * **/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO; // for parsing the CSV file
                                    // You must add this reference to the project.
                                    // To do this, click Project -> Add Reference
                                    // In the reference manager window, click "Assembilies"
                                    // Hit the checkbox on the left near Microsoft.VisualBasic

namespace ECE485_SatHub
{
    // **** STRUCTS AND ENUMERATIONS AND GLOBALS****
    // available to anything in the name space. 

    // An enumeration for the different fields in the .csv file
    // They are ordered according to the column index.
    // E.G time = 0 and the time started in the CSV file resides in column 0. 
    enum FieldTypes { TIME, DEVICE, OPERATION, TS, TR_DATA_TAG };

    // An enumeration for the device specific transfer speeds
    enum TransferRates { MOBILE = 5500000, SATELLITE = 1200 };

    // An Enumeration for the different commands so far.
    enum Commands : byte { SEND, REQUEST, WAIT, JOBDONE, INVALID };
 
    enum PacketSizes { B128, B512, B1024, INVALID };
    
    // ASSUMPTION: Each device link and memory module gets one 16 bit buffer. These
    // buffers can serve many purposes. Here is a rough list of the uses I can think of
    // so far:
    // 1. Data can be held inside as it is transfered in from the serial based links to devices. 
    // 2. Each memory module can use it to hold data? For prefetching or holding data taken
    //      off the data bus, or something else?
    // 3. ???
    // This might be an unnessecary thing to model. I noticed Chars in C# are 16 bits wide.
    // That might do to transfer data back and forth.
    // TODO
    struct Buffer
    {
        // The buffer is 16 bits, or two bytes. 
        // Model the buffer size
        const int BUFFER_WIDTH  = 16; // bits wides
        // What actually goes in here is irrelevant.
        // Except for our command byte and tag byte?
        // ASSUMPTION: 
        // All data transferred will be ascii characters. That means our commands 
        // can be 1 byte followed by the tag information in the second byte. 
        public byte[] _data; 

        // I cannot get set and get to work with converting data types. 
        // This might need to done differently.
        // TODO
        public void SetData(string data)
        {
            int byteSize = BUFFER_WIDTH / 8;
            _data = new byte[byteSize];

            for (int i = 0; i < byteSize; i++)
            {
                _data[i] = (byte)data[i];
            }   

        }

        public byte[] GetData()
        {
            return _data;
        }
    }

    // Struct for memory management directory
    // Keeps the start address and end address
    // Modelling 4 Bytes per DirectoryEntry
    struct DirectoryEntry
    {
        //ushorts are 16 bits wide
        public ushort startAddr;
        public ushort endAddr;
        // 
        public bool allocated;
    }
    
    class Program
    {
        // **** ASSUMPTIONS AND PARAMETERS ****
        // We define the maximum number of devices that can linked to the hub.
        // This includes the satellite and mobile devices.
        const int NUM_DEVICES = 4;
        // assign the satellite device id
        const int SATELLITE_DEVICE_ID = 0;
        // assign depth of memory hierarchy
        // This is the number of levels to the memory
        // For example, a system that has M1, M2, M3 has 
        // a hierarchy depth of 3.
        const int MEM_HIERARCHY_DEPTH = 3;
        // M1 Properties
        const int M1_MODULE_SIZE = 256;
        const int M1_NUM_MODULES = 2;
        const int M1_LATENCY = 1;
        const int M1_COST = 50;
        // M1 Properties
        const int M2_MODULE_SIZE = 512;
        const int M2_NUM_MODULES = 4;
        const int M2_LATENCY = 8;
        const int M2_COST = 10;
        // M1 Properties
        const int M3_MODULE_SIZE = 1024;
        const int M3_NUM_MODULES = 10;
        const int M3_LATENCY = 15;
        const int M3_COST = 3;
        // Packet Size LUT
        static int[] PACKET_SIZE_LUT = { 128, 512, 1024, 0 };
        // Mask for tag (bits 0-4 or 00001111 = 15)
        const byte TAG_MASK = 15;
        // Mask for packet size (bits 5-7 or 11100000 = 224)
        const byte PACKET_SIZE_MASK = 224;
        const int PACKET_SIZE_OFFSET = 5;
        // ASSUMPTION
        // We will never have more than 31 tags
        const int MAX_TAG_VALUE = 31;
        // Our memory managment arrays need to keep track of
        // both send and request commands 'simultaneously'
        const int SEND_INDEX = 0;
        const int REQUEST_INDEX = 1;


        // **** STATIC VARIABLES ****
        // This is a console program, so everything is static? *shrugs* makes sense I guess...

        // This will be the array that holds the different devices connected to the hub.
        // It will include all of the mobile devices as well as the satellite. 
        public static Device[] devices;
        
        // Holds all the different levels of memory in our hierarchy
        public static Memory[] memories;

        // Memory Map breaks up memory into blocks
        public static bool[] memoryMap;

        // The directory for managing data in memory
        public static DirectoryEntry[] MemoryDirectory;

        static void Main(string[] args)
        {
            // PLAYGROUND
            // Test stuff here
            Buffer test = new Buffer();
            test.SetData("AB");
            
            // the components of the sat hub. 
            devices = new Device[NUM_DEVICES];
            memories = new Memory[3];
            MemoryDirectory = new DirectoryEntry[MAX_TAG_VALUE];
            // also let us calculate the maximum memory available
            // for each memory
            int m1MaxMemory = M1_MODULE_SIZE * M1_NUM_MODULES;
            int m2MaxMemory = M2_MODULE_SIZE * M2_NUM_MODULES;
            int m3MaxMemory = M3_MODULE_SIZE * M3_NUM_MODULES;
            int maxTotalMemory = m1MaxMemory + m2MaxMemory + m3MaxMemory;

            ulong tCurrentClock = 0;

            // instatiate components
            // If there are 4 devices, including the satellite, 
            // the first element of the array (@ 0) is the satellite,
            // and the rest are regular mobile devices. 
            // We first instatiate the satellite. 
            devices[SATELLITE_DEVICE_ID] = new Device(SATELLITE_DEVICE_ID, TransferRates.SATELLITE);
            // Then the Device Links
            for(int i = 1; i < NUM_DEVICES; i++)
            {
                devices[i] = new Device(i, TransferRates.MOBILE);
            }
            // Instatiate M1
            memories[0] = new Memory(M1_MODULE_SIZE, M1_NUM_MODULES, M1_LATENCY);
            // Instatiate M2
            memories[1] = new Memory(M2_MODULE_SIZE, M2_NUM_MODULES, M2_LATENCY);
            // Instatiate M3
            memories[2] = new Memory(M3_MODULE_SIZE, M3_NUM_MODULES, M3_LATENCY);

            // Assemble Memory map
            // Block size is smallest packet size. We will use the literal 128 until we find a better way
            // TODO
            int memoryMapSize = maxTotalMemory / 128;
            memoryMap = new bool[memoryMapSize];
            // initialize memory map
            for (int i = 0; i < memoryMapSize; i++)
            {
                memoryMap[i] = false;
            }
            

            // Instatiate and initialize the Memory Directory
            for(int i = 0; i < MAX_TAG_VALUE; i++)
            {
                MemoryDirectory[i] = new DirectoryEntry();
                MemoryDirectory[i].startAddr = 0;
                MemoryDirectory[i].endAddr = 0;
                MemoryDirectory[i].allocated = false;
            }


            // Parse input CSV File
            // Have this be a command line argument in the future.
            string filePath = "final_project_traffic_1.csv";
            ParseTrafficFile(filePath);

            Commands curCmd = Commands.WAIT;
            byte curTag = 0;
            PacketSizes curPackSize = PacketSizes.INVALID;
            // Should this be modeled as hardware somehow?
            // I am still not sure how this will be done.
            // TODO
            Buffer dataFromLink = new Buffer();

            // Keep track of what each current command is for each link
            // since we can handle send and request simultaneously,
            // make it two dimensional with 0 = SEND and 1 = REQUEST

            Commands[,] latestCmds = new Commands[NUM_DEVICES,2];
            byte[,] latestTags = new byte[NUM_DEVICES,2];
            PacketSizes[,] latestPackSize = new PacketSizes[NUM_DEVICES,2];

            // Initialize the tracking elements
            for(int i = 0; i < NUM_DEVICES; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    latestCmds[i,j] = new Commands();
                    latestCmds[i,j] = Commands.WAIT;
                    latestTags[i,j] = new byte();
                    latestTags[i,j] = 0;
                    latestPackSize[i,j] = new PacketSizes();
                    latestPackSize[i,j] = PacketSizes.INVALID;
                }
            }


            // The loop that simulates each clock cycle. 
            // This condition needs some more development, must end sometime
            // TODO
            while (true)
            {

                // Reap memory section



                // Service the Satellite
                // ?? We will need to add Events to the Satellite device somehow...
                // TODO

                for(int i = 1; i < NUM_DEVICES; i++)
                {
                    dataFromLink = devices[i].Service(tCurrentClock);

                    // extract command, tag, and packet size from the data
                    // we will later check if it is a valid command or actual data
                    // If there is a command in the data from the link
                    // It will be in the first byte. We can check there 
                    // for a command. If the data from link is simply ascii data,
                    // it will not match any of our command bytes.
                    curCmd = (Commands)dataFromLink._data[0];
                    // bits 0 - 4 contain the tag (00001111 = 15)
                    curTag = (byte)(dataFromLink._data[1] & TAG_MASK);
                    // bit 5-7 contain the packet size
                    curPackSize = (PacketSizes)((dataFromLink._data[1] & PACKET_SIZE_MASK) >> PACKET_SIZE_OFFSET);

                    // if there is nothing from the device, continue to the next device
                    // This state is represented by the WAIT command, signifiying that 
                    // the device link is either idle or waiting for data.
                    // Continues are not good yes? Can we rewrite this in a different way?
                    // TODO
                    if (curCmd == Commands.WAIT)
                    {
                        continue;
                    }

                    // On the first clk from a command we save the current command, tag and packet size
                    if(curCmd == Commands.REQUEST || curCmd == Commands.SEND)
                    {
                        // we can use the enumeration value for the type of command
                        // index. This is a nasty 'clever' bit of code. Sorry. 
                        latestCmds[i, (int)curCmd] = curCmd;
                        latestTags[i, (int)curCmd] = curTag;
                        latestPackSize[i, (int)curCmd] = curPackSize;

                        int numBlocks = PACKET_SIZE_LUT[(int)curPackSize] / 128;
                        
                        
                        // allocate the memory
                        // This should technically take 1 clock cycle
                        // TODO
                        bool success = AllocateMemory(memoryMapSize, numBlocks);

                        // if we could not successfully find a block
                        // stall the device
                        if (!success)
                        {
                            devices[i].Stall(tCurrentClock);
                            // start evicting stuff
                        }
                    }

                    // Memory reap section
                    // Check if the memory has any data ready for us.

                    // If the data does not contain a command, then it is pure data
                    if(!Enum.IsDefined(typeof(Commands), curCmd))
                    {
                        // find out what we are doing with the data from the latest command information
                        //lastCommand = latestCmds[
                        
                        // for a send, we calculate the address and then write it to memory
                        // TODO this means we have to keep track of not only the latest command
                        // but our progress in tranfering the latest command's data to memory.
                        
                        // for a request, we calculate the address (see read) and tell
                        // the memory to give up the goods. After the latency, we will have to
                        // reap the information from the memory. (See memory request reap section).

                        // TODO What happens if we have a command and also must reap memory
                        // at the same time. Obviously, we reap first and then issue the command
                        // on the next clock cycle. The chance of this happening is really low. 
                        // this is not currently reflected in the logic. FIX THIS
                    }

                    


                }

                tCurrentClock++;
            }
            
        }

        private static bool AllocateMemory(int memoryMapSize, int numBlocks)
        {
            int block;
            bool success = false;

            // now we find a spot in our memory
            for (block = 0; block < memoryMapSize && !success; block++)
            {
                // check if unallocated
                if (memoryMap[block] == false)
                {
                    // assume the necessary sequential blocks are also free 
                    success = true;
                    // actually check if sequential blocks are available
                    for (int seqBlock = 1; seqBlock < numBlocks && success; seqBlock++)
                    {
                        // if it isn't free
                        if (memoryMap[block + seqBlock] != false)
                        {
                            // the chunk of memory was not big enough
                            success = false;
                        }
                    }
                }
            }

            if (success) ;
            {
                // allocate the memory
                for (int num = 0; num < numBlocks; num++)
                {
                    memoryMap[block + num] = true;
                }
            }
            return success;
        }

        // Parses the traffic csv file, and initializes all of the
        // device events.
        private static void ParseTrafficFile(string filePath)
        {
            TextFieldParser parser = new TextFieldParser(filePath);
            parser.SetDelimiters(",");

            // Skip over header line.
            parser.ReadLine();

            while (!parser.EndOfData)
            {

                string[] fields = parser.ReadFields();

                // Device id for the Event
                int deviceId = int.Parse(fields[(int)FieldTypes.DEVICE]);

                // Add the event to the corresponding device. 
                devices[deviceId].AddEvent(
                                            int.Parse(fields[(int)FieldTypes.TIME]),
                                            fields[(int)FieldTypes.OPERATION],
                                            int.Parse(fields[(int)FieldTypes.TS]),
                                            int.Parse(fields[(int)FieldTypes.TR_DATA_TAG])
                                          );
            }
        }
    }
}


/**
 * RUBBISH BIN
 * Before I send something off to delete land, I put it at the end of the file. This is my 'rubbish bin'. 
 * After I am sure that it is no longer needed (absolutely sure!) do I hit that delete key.
 * 
 *  // I am not sure how to model the atomic size of data we can transfer. 
    // We know we have a bus width of 16 bits.
    // So I imagine we should use the full bus to transfer the data. 
    // But it may be that the bus width is the most important 
    // detail, since that determines the smallest step possible in data transfer.
    // There is actually no data being transfered, so what the data is is irrelevant.
    // The data tag is important, but that should be stored in some sort of memory 
    // management, and not the main memory itself. 
    // TODO
    struct AtomicTransfer
    {
        // For now, lets just model the two bytes required for a data transfer to memory.
        // This atomic transfer can be modeled as the buffer? WAIT. MAYBE MODELING BUFFER IS BETTER.
        // Or address bus? 
        char[] data = new char[2];
    }
 * 
 * 
 * //Commands 
                    //foreach(Commands cmd in Enum.GetValues(typeof(Commands)))
                    //{

                    //}

                    //switch (dataFromLink._data[0])
                    //{
                    //    case (byte)Commands.REQUEST:
                    //        // start getting data from memory
                    //        // TODO
                    //        break;
                    //    case (byte)Commands.SEND:
                    //        // start putting data into memory
                    //        // TODO
                    //        break;
                    //    case (byte)Commands.WAIT:
                    //        // we are waiting for a data transfer to complete
                    //        // TODO
                    //        break;
                    //    case (byte)Commands.JOBDONE:
                    //        // This device has finished all events. 
                    //        // When all are done we want to flip some switch that exits the loop
                    //        break;
                    //}
 * 
 *   // Then, some lengthy amount of clk after recieving a send/request cmd
                    // , we will recieve the size of the packet.
                    // We know this state because we will have saved a send/request but packetsize 
                    // will be invalid.
                    // TODO
                    // Is there a way that we can fit command, tag, and packet size into 16 bits?
                    bool recievedPacketSize = currentCmds[i] == Commands.SEND || currentCmds[i] == Commands.REQUEST;
                    recievedPacketSize = recievedPacketSize && currentPackSize[i] == PacketSizes.INVALID;
                    if (recievedPacketSize)
                    {
                        //currentPackSize[i] = (PacketSizes)dataFromLink[0];
                    }
 * 
 * 
                    //bool success = false;
                    // If there is Data from the link, pass it to the memory
                    //for (int memLevel = 0; memLevel < MEM_HIERARCHY_DEPTH && !success; memLevel++)
                    //{
                    //    // 
                    //}
**/