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

    struct MemoryManagerElement
    {
        // Our Memory Managment Unit uses these Memory Managment Units
        // to keep track of what tags are currently in memory
        public int tag;
        // what size they are
        public int size;
        // how recently they were used. Higher is older. 
        public int lruValue;
        // and if they currently reside in our local memory.
        public bool citizen;
        // also mark the time the memory is valid and complete in memory
        public ulong tClkFinish;
        // and for when we must stall a SEND device but 
        // do not want to go to the data center when a request for its data
        // arrives. 
        public bool incoming;
        
    }
    
    class Program
    {
        // **** ASSUMPTIONS AND PARAMETERS ****
        // We define the maximum number of devices that can linked to the hub.
        // This includes the satellite and mobile devices.
        const int NUM_DEVICES = 8;
        // assign the satellite device id
        const int SATELLITE_UPLINK_ID = 0;
        const int SATELLITE_DOWNLINK_ID = 4;
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
        public static List<Event> _listOfEvents;
        public static ulong tCurrentClock;
        public static int numEvents;

        // this will use the Tag as the index, and store the last time it was used. 
        // It is used for the LRU policy. 
        public static MemoryManagerElement[] memoryManagementUnit;

        // Keep track of the allocated memory in bytes.
        public static int allocatedMemory;

        static void Main(string[] args)
        {
            // PLAYGROUND
            // Test stuff here

         
            // the components of the sat hub. 
            devices = new Device[NUM_DEVICES];
            _listOfEvents = new List<Event>();

            // also let us calculate the maximum memory available
            // for each memory
            int m1MaxMemory = M1_MODULE_SIZE * M1_NUM_MODULES;
            int m2MaxMemory = M2_MODULE_SIZE * M2_NUM_MODULES;
            int m3MaxMemory = M3_MODULE_SIZE * M3_NUM_MODULES;
            int maxTotalMemory = m1MaxMemory + m2MaxMemory + m3MaxMemory;

            tCurrentClock = 0;
            allocatedMemory = 0;

            // instatiate components
            // If there are 4 devices, including the satellite, 
            // the first element of the array (@ 0) is the satellite uplink,
            // and 1-3 are regular mobile device downlinks.
            // Then 4 is the satellite downlink and 1-3 are mobile device uplinks.
            // We first instatiate  Device Links 
            // This way, the logic of our loop is kept cleaner.
            for(int i = 1; i < NUM_DEVICES; i++)
            {
                devices[i] = new Device(i, TransferRates.MOBILE);
            }
            // Then the satelliteup and down links
            devices[SATELLITE_UPLINK_ID] = new Device(SATELLITE_UPLINK_ID, TransferRates.SATELLITE);
            devices[SATELLITE_DOWNLINK_ID] = new Device(SATELLITE_DOWNLINK_ID, TransferRates.SATELLITE);

            // initialize MMU
            memoryManagementUnit = new MemoryManagerElement[MAX_TAG_VALUE];
            for(int i = 0; i < MAX_TAG_VALUE; i++)
            {
                memoryManagementUnit[i] = new MemoryManagerElement();
                memoryManagementUnit[i].tag = i;
                memoryManagementUnit[i].lruValue = 0;
                memoryManagementUnit[i].size = 0;
                memoryManagementUnit[i].citizen = false;
                memoryManagementUnit[i].tClkFinish = 0;
                memoryManagementUnit[i].incoming = false;

            }

            // Parse input CSV File
            // Have this be a command line argument in the future.
            string filePath = "final_project_traffic_1.csv";
            ParseTrafficFile(filePath);

            ulong clockCheck = 1;
           // bool notFinished = true;
            // The loop that simulates each clock cycle. 
            // This condition needs some more development, must end sometime
            // TODO
            while (tCurrentClock < 1000000000)
            {
                //notFinished = false;
                // this way of going through our list of events might be really expensive and slow...
                foreach (Event aEvent in _listOfEvents.ToList())
                {
                    // assign the device id
                    int deviceId = aEvent._deviceId;
                    if(aEvent._operation == "REQUEST")
                    {
                        deviceId += 4;
                    }

                    if (deviceId == SATELLITE_UPLINK_ID)
                    {
                       //Console.WriteLine("I am a satellite uplink event");
                    }

                    // check if the event has started yet and not ended yet.
                    if (tCurrentClock >= aEvent._tClockStart && aEvent._tClockEnd == 0)
                    {
                        ulong additionalLatency = 0;
                        //notFinished = true;

                        if (devices[deviceId].linkOccupiedBy == -1 && HandleEvent(aEvent, maxTotalMemory, ref additionalLatency))
                        {
                            // see if the device link is available and if the hub can handle the request
                            // assign the link to this event
                            devices[deviceId].linkOccupiedBy = aEvent._eventId;
                            // and calculate when the link will be finished. 
                            ulong tClockEnd = tCurrentClock + additionalLatency + devices[deviceId].CalculateLatency(aEvent._transactionSize);
                            aEvent._tClockEnd = tClockEnd;
                            memoryManagementUnit[aEvent._trDataTags].tClkFinish = tClockEnd;
                            memoryManagementUnit[aEvent._trDataTags].citizen = true;
                            memoryManagementUnit[aEvent._trDataTags].size = aEvent._transactionSize;
                            ReplacementPolicyUpdate(aEvent);
                            PrintMsg("START ", aEvent);
                        }
                        else
                        {
                            // mark SEND data as incoming even though we cannot currently handle it
                            if (aEvent._operation == "SEND")
                            {
                                memoryManagementUnit[aEvent._trDataTags].incoming = true;
                            }
                        }
                    }
                    else if (tCurrentClock >= aEvent._tClockEnd && devices[deviceId].linkOccupiedBy == aEvent._eventId)
                    {
                        //notFinished = true;
                        // the event has ended but is still occupying the link, 
                        // unassign the device link from it.
                        devices[deviceId].linkOccupiedBy = -1;
                        // mark the memory.
                        // if it was our satellite that finished
                        if(deviceId == SATELLITE_UPLINK_ID)
                        {
                            // evict the memory
                            allocatedMemory -= aEvent._transactionSize;
                        }
                        PrintMsg("Finished ", aEvent);
                    } 
                    // go head and register the data in the event as a resident citizen,
                    // so that we do not go to the data center for it later on.
                    // memoryManagementUnit[aEvent._trDataTags].citizen = true;
                }

                tCurrentClock++;
                if(tCurrentClock == Math.Pow(10, clockCheck))
                {
                    Console.WriteLine("Current Clock at " + tCurrentClock);
                    clockCheck++;
                }
            }
            WriteResultsFile(@"final_project_traffic_1_results.csv");
            Console.WriteLine("FINISHED!!!!");
        }

        private static void ReplacementPolicyUpdate(Event aEvent)
        {
            // increase the lru value of all tags held in the MMU
            for (int j = 0; j < MAX_TAG_VALUE; j++)
            {
                if (memoryManagementUnit[j].citizen)
                {
                    memoryManagementUnit[j].lruValue++;

                }
            }
            // set the lru value of the tag we just hit to 0
            memoryManagementUnit[aEvent._trDataTags].lruValue = 0;
        }

        private static void PrintMsg(string state, Event aEvent)
        {
            Console.WriteLine(state +  (aEvent._eventId + 1) + " at tClk " + tCurrentClock + 
                " with plans to end at " + aEvent._tClockEnd);
        }

        private static bool HandleEvent(Event aEvent, int maxTotalMemory, ref ulong additionalLatency)
        {
            additionalLatency = 0;
            bool success = false;

            // if the event is a send
            if (aEvent._operation == "SEND")
            {
                // check if we have available memory
                if (maxTotalMemory - allocatedMemory >= aEvent._transactionSize)
                {
                    allocatedMemory += aEvent._transactionSize;
                    success = true;
                }
                else if (devices[SATELLITE_UPLINK_ID].linkOccupiedBy == -1)
                {
                    // No room, but the sat uplink is available
                    additionalLatency += Evict();
                    success = true;
                }
                // memory is full, satellite uplink is unavailable, stall device
                //PrintMsg("Stall SEND device, Mem full, sat busy ", aEvent);
            } 
            else
            {
            // if the event is a request
            // check if the tag is in our memory
                if(memoryManagementUnit[aEvent._trDataTags].citizen)
                {
                    // it is, we are successful
                    if (memoryManagementUnit[aEvent._trDataTags].tClkFinish <= tCurrentClock)
                    {
                        success = true;
                    }
                } 
                else if (devices[SATELLITE_DOWNLINK_ID].linkOccupiedBy == -1 && !memoryManagementUnit[aEvent._trDataTags].incoming)
                {
                    // If the data is not coming from a device later, we can get it from the data center
                    // as well as if we have memory to recieve it
                    if(maxTotalMemory - allocatedMemory >= aEvent._transactionSize)
                    {
                        // wonderful, we have space, we can first get it from the satellite. 
                        additionalLatency += GetFromDataCenter(aEvent);
                        success = true;
                    }
                    else if (devices[SATELLITE_UPLINK_ID].linkOccupiedBy == -1)
                    {
                        // ungh... this is where things get complicated.
                        // We need to evict things to make room for what we want from the data center.
                        // The uplink is free, so we can go ahead and do that.
                        // Having this in here makes me think there is still a logically simplier way.
                        additionalLatency += Evict();
                        additionalLatency += GetFromDataCenter(aEvent);
                        success = true;
                    }
                    // PrintMsg("Stall REQUEST device, Mem full, sat busy ", aEvent);
                    // Both satellite links are busy. 
                    // Memory is full.
                    // Try again later. 
                }
            }
            return success;
        }

        private static ulong GetFromDataCenter(Event aEvent)
        {
            ulong additionalLatency = 0;

            Event pullDown = new Event(
                                        tCurrentClock,
                                        "REQUEST",
                                        SATELLITE_UPLINK_ID,            // because we add 4 to requests in the main loop
                                        aEvent._transactionSize,
                                        aEvent._trDataTags,
                                        ++numEvents
                                       );
            _listOfEvents.Add(pullDown);
            devices[SATELLITE_DOWNLINK_ID].linkOccupiedBy = numEvents;
            additionalLatency = devices[SATELLITE_DOWNLINK_ID].CalculateLatency(aEvent._transactionSize);
            pullDown._tClockEnd = tCurrentClock + additionalLatency;

            return additionalLatency;
        }

        private static ulong Evict()
        {
            ulong additionalLatency;

            // memory is full, satellite uplink is available, start evicting
            int tagToEvict = FindOldestTag();
            // start an event to transfer the memory out to the data center
            // using the satellite hub
            Event eviction = new Event(
                                        tCurrentClock,
                                        "SEND",
                                        SATELLITE_UPLINK_ID,
                                        memoryManagementUnit[tagToEvict].size,
                                        tagToEvict,
                                        ++numEvents
                                       );
            // once the eviction has started, the block is no longer valid.
            // but we must wait until the eviction event is finished before 
            memoryManagementUnit[tagToEvict].citizen = false;
            memoryManagementUnit[tagToEvict].tClkFinish = 0;
            _listOfEvents.Add(eviction);
            devices[SATELLITE_UPLINK_ID].linkOccupiedBy = numEvents;
            additionalLatency = devices[SATELLITE_UPLINK_ID].CalculateLatency(memoryManagementUnit[tagToEvict].size);
            eviction._tClockEnd = tCurrentClock + additionalLatency;
            PrintMsg("Eviction ", eviction);
            return additionalLatency;
        }

        private static int FindOldestTag()
        {
            // find the oldest tag in memory
            // for implementing LRU replacement policy.
            // We should not have to account for the time it takes for this complete.
            // I believe there is hardware support LRU policies that use triggered counters
            // and compare registers. 
            int oldest = -1;
            for (int i = 0; i < MAX_TAG_VALUE; i++)
            {
                if (memoryManagementUnit[i].citizen)
                {
                    if (oldest == -1)
                    {
                        oldest = i;
                    }
                    if (memoryManagementUnit[i].lruValue > memoryManagementUnit[oldest].lruValue)
                    {
                        oldest = i;
                    }
                }
            }

            return oldest;
        }

        // Write the results file
        private static void WriteResultsFile(string filePath)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(filePath, true))
            {
                ulong latency;
                file.WriteLine("time, device, operation, ts, tr_data_tag, time_end, latency");
                foreach (Event aEvent in _listOfEvents)
                {
                    latency = aEvent._tClockEnd - aEvent._tClockStart;
                    file.WriteLine(
                        aEvent._tClockStart +
                        ", " + aEvent._deviceId +
                        ", " + aEvent._operation +
                        ", " + aEvent._transactionSize +
                        ", " + aEvent._trDataTags +
                        ", " + aEvent._tClockEnd +
                        ", " + latency);
                }
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

            int i = 0;
            while (!parser.EndOfData)
            {

                string[] fields = parser.ReadFields();

                // allocate the memory for the event and add it to the list. 
                Event eventToAdd = new Event(
                                            ulong.Parse(fields[(int)FieldTypes.TIME]),
                                            fields[(int)FieldTypes.OPERATION],
                                            int.Parse(fields[(int)FieldTypes.DEVICE]),
                                            int.Parse(fields[(int)FieldTypes.TS]),
                                            int.Parse(fields[(int)FieldTypes.TR_DATA_TAG]),
                                            i
                                            );
                _listOfEvents.Add(eventToAdd);
                i++;
            }
            numEvents = i;
        }
    }
}
