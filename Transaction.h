#pragma once
enum Operation { SEND, RECIEVE};
enum TransactionSize { B128 = 128, B512 = 512, B1024 = 1024 };  

class Transaction
{
private:
	// *** DATA MEMBERS ***
	// Time the transaction is attempted by the device
	// to start. 
	unsigned long long _tClockStart;
	// Data tag, tr_data_tag, Max value is 31
	unsigned short _trDataTag;
	//
	unsigned short _device;
	//
	Operation _command;
	//
	TransactionSize _transactionSize;
	// Keep track of the number of bytes left to store in memory
	// When 0, the transaction has been finished.
	unsigned short _numBytesLeft;
	// Store the number of clock cycles the transaction finished
	unsigned long long _tClockFinished;
	// Store the entire amount of clock cycles the transaction took
	unsigned long long _tClockTotal;


	// *** FUNCTIONS ***


public:
	Transaction(void);
	~Transaction(void);

	// Check the transaction.
	// First see if the transaction has started yet
	// If so, update it (TBD).
	void Check(unsigned long long tClockCurrent);
};

