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

    // an enumeration for our different replacement policies
    //enum ReplacementPolicies : string {LRU = "LRU", MRU = "MRU", SNOOP_QUEUE = "QUEUE"}; 

    // What this does is model what each MMU keeps track of for each tag in memory.
    // Really, the MMU should be it own class that has an interal array of these
    // and AllocateMemory should be a function. TODO
    struct MemoryManagerElement
    {
        // a static variable that keeps the size of the current unhandled request queue
        static public int requestQueueSize;

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
        // if keep track of if and where the item is in the unhandled requests queue.
        // 0 is not in the unhandled requests queue
        public int requestQueuePos;
        
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
        static int M1_NUM_MODULES = 2;
        const int M1_LATENCY = 1;
        const int M1_COST = 50;
        // M1 Properties
        const int M2_MODULE_SIZE = 512;
        static int M2_NUM_MODULES = 4;
        const int M2_LATENCY = 8;
        const int M2_COST = 10;
        // M1 Properties
        const int M3_MODULE_SIZE = 1024;
        static int M3_NUM_MODULES = 10;
        const int M3_LATENCY = 15;
        const int M3_COST = 3;
        // Packet Size LUT
        static int[] PACKET_SIZE_LUT = { 128, 512, 1024, 0 };

        // ASSUMPTION
        // We will never have more than 32 tags
        const int MAX_TAG_VALUE = 32;
        // Our memory managment arrays need to keep track of
        // both send and request commands 'simultaneously'
        const int SEND_INDEX = 0;
        const int REQUEST_INDEX = 1;

        // REPLACEMENT POLICIES;
        const string R_LRU = "LRU";
        const string R_MRU = "MRU";
        const string R_SNOOP_QUEUE = "QUEUE";


        // **** STATIC VARIABLES ****
        // This is a console program, so everything is static? *shrugs* makes sense I guess...

        // This will be the array that holds the different devices connected to the hub.
        // It will include all of the mobile devices as well as the satellite. 
        public static Device[] devices;
        public static List<Event> _listOfEvents;
        public static ulong tCurrentClock;
        public static int numEvents;
        public static int completedEvents;
        public static string replacementPolicy;


        // this will use the Tag as the index, and store the last time it was used. 
        // It is used for the LRU policy. 
        public static MemoryManagerElement[] memoryManagementUnit;

        // Keep track of the allocated memory in bytes.
        public static int allocatedMemory;

        static void Main(string[] args)
        {
            string filePath = "final_project_traffic_1.csv";
            replacementPolicy = R_SNOOP_QUEUE;

            // get cmdline args
            if(args.Length == 5)
            {
                filePath = args[0];
                replacementPolicy = args[1];
                M1_NUM_MODULES = Convert.ToInt32(args[2]);
                M2_NUM_MODULES = Convert.ToInt32(args[3]);
                M3_NUM_MODULES = Convert.ToInt32(args[4]);
               
            }

            Console.WriteLine("For traffic file " + filePath + " and M1_NUM_MODULES = " + M1_NUM_MODULES + " M2 = " + M2_NUM_MODULES + " M3 " + M3_NUM_MODULES);
         
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
            completedEvents = 0;

            // instatiate components
            // If there are 4 devices, including the satellite, 
            // the first element of the array (@ 0) is the satellite uplink,
            // and 1-3 are regular mobile device downlinks.
            // Then 4 is the sategllite downlink and 5-7 are mobile device uplinks.
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
            // initialize the MMU request queue size tracker 
            // it is a static variable, so it we set it in the struct type for the element.
            MemoryManagerElement.requestQueueSize = 0;

            // Parse input CSV File
            ParseTrafficFile(filePath);

            ulong clockCheck = 1;
           // bool notFinished = true;
            // The loop that simulates each clock cycle. 
            // This condition needs some more development, must end sometime
            // TODO
            while (completedEvents < numEvents)
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
                        // ********** EVENT READY ************
                        // each new event probably interrupts a current event with the device in question
                        // so, we start off with an additional latency of 1 to account for this interrupt.
                        // No matter what, when that event eventually starts, the command will have to be 
                        // sent again, so it does not even out. 
                        ulong additionalLatency = 1;
                        //notFinished = true;

                        // If the device link is available and then if the hub can handle the request
                        // The order of this actually is important and is a bit of C# exploit
                        // Because if we enter the allocate memory method for a satellite device there will be bugs.
                        // This is because satellite events are started in AllocateMemory to keep
                        // the logic in this loop simplier. Entering allocatememory for a sat device would become circular.
                        // The way the exploit works is that when a satellite event is started in Allocate memory
                        // the corresponding satellite link is occupied by that event. Therefore, satellite events
                        // will never pass the link unoccupied check. In C#, the precedence of boolean comparisons 
                        // is left to right. If the leftmost of the && below fails, the right side is never evaulated.
                        // Therefore the allocate memory will never be entered by an event owned by a satellite device.
                        if (devices[deviceId].linkOccupiedBy == -1 && AllocateMemory(aEvent, maxTotalMemory, ref additionalLatency))
                        {
                            /// ************ START EVENT *************
                            // see if the 
                            // assign the link to this event
                            devices[deviceId].linkOccupiedBy = aEvent._eventId;
                            // and calculate when the link will be finished. 
                            ulong tClockEnd = tCurrentClock + additionalLatency + devices[deviceId].CalculateLatency(aEvent._transactionSize);
                            aEvent._tClockEnd = tClockEnd;
                            // update our MMU
                            memoryManagementUnit[aEvent._trDataTags].tClkFinish = tClockEnd;
                            memoryManagementUnit[aEvent._trDataTags].citizen = true;
                            memoryManagementUnit[aEvent._trDataTags].size = aEvent._transactionSize;
                            // update the replacement policy.
                            ReplacementPolicyUpdate(aEvent);

                            PrintMsg("START ", aEvent);
                        }
                        else
                        {
                            // *************** UNABLE TO HANDLE ******************
                            // we cannot handle currently handle the command from the device
                            // however, if this is the first time that we have recieved the command
                            // mark SEND data as incoming even though we cannot currently handle it
                            if (aEvent._operation == "SEND" && !memoryManagementUnit[aEvent._trDataTags].incoming)
                            {
                                memoryManagementUnit[aEvent._trDataTags].incoming = true;
                            }
                            else if (aEvent._operation == "REQUEST" && !memoryManagementUnit[aEvent._trDataTags].incoming)
                            { 
                                // it is a request
                                // put it into the queue
                                memoryManagementUnit[aEvent._trDataTags].requestQueuePos = ++MemoryManagerElement.requestQueueSize;
                                memoryManagementUnit[aEvent._trDataTags].incoming = true;
                            }
                        }
                    }
                    else if (tCurrentClock >= aEvent._tClockEnd && devices[deviceId].linkOccupiedBy == aEvent._eventId)
                    {
                        // ********** EVENT FINISHED **************
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
                        // mark the data as no longer incoming
                        memoryManagementUnit[aEvent._trDataTags].incoming = false;
                        PrintMsg("Finished ", aEvent);
                        completedEvents++;
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
            WriteResultsFile(
                            "RESULTS_" + 
                            replacementPolicy + "_" + 
                            M1_NUM_MODULES + "_" + 
                            M2_NUM_MODULES + "_" + 
                            M3_NUM_MODULES + "_" + 
                            filePath
                            );
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
		//Prints event info
        private static void PrintMsg(string state, Event aEvent)
        {
            Console.WriteLine(state +  (aEvent._eventId + 1) + " at tClk " + tCurrentClock + 
                " with plans to end at " + aEvent._tClockEnd);
        }

        // ************* MEMORYALLOCATE **************
        // Check if we can allocate memory for the queued transaction
        // Returns true if we can, otherwise returns false
        // Takes in additional latency by reference and will 
        // add any additional latencies the allocation results in
        // (for example, if we evict memory to make room, the latency of the 
        // of the eviction process will be added to additionalLatency)
        // 
        private static bool AllocateMemory(Event aEvent, int maxTotalMemory, ref ulong additionalLatency)
        {
            //additionalLatency = 0;
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
                    additionalLatency += Evict(aEvent._transactionSize);
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
                        // The uplink is free, so we can go ahead and do that, allowing us to request
                        // the data from the satellite.
                        // Having this in here makes me think there is still a logically simplier way.
                        additionalLatency += Evict(aEvent._transactionSize);
                        additionalLatency += GetFromDataCenter(aEvent);
                        success = true;
                    }
                    // PrintMsg("Stall REQUEST device, Mem full, sat busy ", aEvent);
                    // Both satellite links are busy. 
                    // Memory is full.
                    // Stall initiating transfer
                    // success is still false from initialzation.

                }
            }
            return success;
        }

        // ************** GET DATA FROM DATA CENTER ***************
        // Starts a sat downlink event to grab a tag from the data center.
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
            PrintMsg("Pulling from Datacenter ", pullDown);

            return additionalLatency;
        }

        // ****************** EVICT A TAG FROM MEMORY *****************
        private static ulong Evict(int ts)
        {
            ulong additionalLatency;

            // memory is full, satellite uplink is available, start evicting
            int tagToEvict = ExecuteReplacementPolicy(ts);
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

        // ************** REPLACEMENT POLICY SWITCH *************
        // Will exectue the chosen replacement policy.
        private static int ExecuteReplacementPolicy(int ts)
        {
            if (replacementPolicy == R_LRU)
            {
                return FindOldestTag(ts);
            }
            else if (replacementPolicy == R_MRU)
            {
                return FindYoungestTag(ts);
            }
            else if (replacementPolicy == R_SNOOP_QUEUE)
            {
                return SnoopRequestQueue(ts);
            }
            else
            {
                // default to LRU
                Console.Write("INVALID REPLACEMENT POLICY -- USING LRU");
                return FindOldestTag(ts);
            }
        }

        // Our unique Replacement for this specific setup.
        // ASSUMPTION: We have recieved many commands we unable to process because the links are so slow.
        // If we kept track of these commands, we can look at what unhandled requests reference tags
        private static int SnoopRequestQueue(int ts)
        {
            // first grab the Least Recently Used tag
            int tagToEvict = FindOldestTag(ts);
            // check if that tag is in the request queue and is still incoming
            if (memoryManagementUnit[tagToEvict].incoming = true && memoryManagementUnit[tagToEvict].requestQueuePos > 0)
            {
                // Do not evict that tag
                tagToEvict = -1;
                //  Instead, first search for a tag of equal size that is not in the request queue
                for (int i = 0; i < MAX_TAG_VALUE; i++)
                {
                    // if we have the tag in memory, it is the right size, and is not the current request queue
                    if (memoryManagementUnit[i].citizen && memoryManagementUnit[i].size >= ts && memoryManagementUnit[tagToEvict].requestQueuePos == 0)
                    {
                        // use it
                        tagToEvict = i;
                    }
                }
                // if we did not find anything, search for the oldest tag in the request queue
                if (tagToEvict == -1)
                {
                    for (int i = 0; i < MAX_TAG_VALUE; i++)
                    {
                        // if we have the tag in memory, it is the right size, and is not the current request queue
                        if (memoryManagementUnit[i].citizen && memoryManagementUnit[i].size >= ts)
                        {
                            // first valid canidate
                            if (tagToEvict == -1)
                            {
                                tagToEvict = i;
                            }
                            if (memoryManagementUnit[i].requestQueuePos > memoryManagementUnit[tagToEvict].requestQueuePos)
                            {
                                tagToEvict = i;
                            }
                        }
                    }
                }
               
            }
            return tagToEvict;
        }

        private static int FindYoungestTag(int ts)
        {
            // find the oldest tag in memory
            // for implementing LRU replacement policy.
            // We should not have to account for the time it takes for this complete.
            // I believe there is hardware support LRU policies that use triggered counters
            // and compare registers. 
            int youngest = -1;
            for (int i = 0; i < MAX_TAG_VALUE; i++)
            {
                if (memoryManagementUnit[i].citizen && memoryManagementUnit[i].size >= ts)
                {
                    // first evict canidate
                    if (youngest == -1)
                    {
                        youngest = i;
                    }
                    if (memoryManagementUnit[i].lruValue < memoryManagementUnit[youngest].lruValue)
                    {
                        youngest = i;
                    }
                }
            }

            return youngest;
        }

        private static int FindOldestTag(int ts)
        {
            // find the oldest tag in memory
            // for implementing LRU replacement policy.
            // We should not have to account for the time it takes for this complete.
            // I believe there is hardware support LRU policies that use triggered counters
            // and compare registers. 
            int oldest = -1;
            for (int i = 0; i < MAX_TAG_VALUE; i++)
            {
                // if the tag resides in memory and is the same size
                if (memoryManagementUnit[i].citizen && memoryManagementUnit[i].size >= ts)
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
