#pragma once
#include "Transaction.h"
#include "Memory.h"
#define NUMBER_OF_TRANSACTIONS = 32

class SatelliteHub
{
private:
	// **** DATA MEMBERS ****
	// Holds all of the transactions
	// Maybe make this dynamically allocated to the number of transactions in the file.
	Transaction *lTransactions;
	
	// Maybe make these into an array
	Memory * _m1;
	Memory * _m2;
	Memory * _m3;

	// Lets be safe with the upper limits on the clock
	unsigned long long _tClock;

	// For the transaction .csv file
	char * _fileName;

	// **** FUNCTIONS ****
	// This will grab all of the transactions from the .csv file.
	void GetListOfTransactions();

	// Runs through a single clock cycle
	void RunClockCycle();

public:
	SatelliteHub(void);
	~SatelliteHub(void);
};

