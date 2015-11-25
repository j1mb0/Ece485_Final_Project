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
    enum Commands : byte { SEND, REQUEST, WAIT, JOBDONE };

    enum PacketSizes { B128 = 128, B512 = 512, B1024 = 1024 };
    
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
    
    class Program
    {
        // **** ASSUMPTIONS AND PARAMETERS ****
        // We define the maximum number of devices that can linked to the hub.
        // This includes the satellite and mobile devices.
        const int NUM_DEVICES = 4;
        // assign the satellite device id
        const int SATELLITE_DEVICE_ID = 0;

        // **** STATIC VARIABLES ****
        // This is a console program, so everything is static? *shrugs* makes sense I guess...

        // This will be the array that holds the different devices connected to the hub.
        // It will include all of the mobile devices as well as the satellite. 
        public static Device[] devices;
        public static Memory[] memories;

        static void Main(string[] args)
        {
            // PLAYGROUND
            // Test stuff here
            Buffer test = new Buffer();
            test.SetData("AB");
            
            // the components of the sat hub. 
            devices = new Device[NUM_DEVICES];
            memories = new Memory[3];

            ulong tCurrentClock = 0;

            // instatiate components
            // If there are 4 devices, including the satellite, 
            // the first element of the array (@ 0) is the satellite,
            // and the rest are regular mobile devices. 
            // We first instatiate the satellite. 
            devices[SATELLITE_DEVICE_ID] = new Device(SATELLITE_DEVICE_ID, TransferRates.SATELLITE);

            for(int i = 1; i < NUM_DEVICES; i++)
            {
                devices[i] = new Device(i, TransferRates.MOBILE);
            }

            // Parse input CSV File
            // Have this be a command line argument in the future.
            string filePath = "final_project_traffic_1.csv";
            ParseTrafficFile(filePath);

            Commands curCmd = Commands.WAIT;
            // Should this be modeled as hardware somehow?
            // I am still not sure how this will be done.
            // TODO
            Buffer dataFromLink = new Buffer();

            // The loop that simulates each clock cycle. 
            // The majority of the program is going to be spent going through this.
            // Right now it will break out of it when the first device finishes
            // So this condition needs some more development
            // TODO
            while (curCmd != Commands.JOBDONE)
            {
                // Service the Satellite
                // ?? We will need to add Events to the Satellite device somehow...
                // TODO

                for(int i = 1; i < NUM_DEVICES; i++)
                {
                    dataFromLink = devices[i].Service(tCurrentClock);

                    switch (dataFromLink._data[0])
                    {
                        case (byte)Commands.REQUEST:
                            // start getting data from memory
                            // TODO
                            break;
                        case (byte)Commands.SEND:
                            // start putting data into memory
                            // TODO
                            break;
                        case (byte)Commands.WAIT:
                            // we are waiting for a data transfer to complete
                            // TODO
                            break;
                        case (byte)Commands.JOBDONE:
                            // This device has finished all events. 
                            // When all are done we want to flip some switch that exits the loop
                            break;
                    }
                }

                tCurrentClock++;
            }
            
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
**/