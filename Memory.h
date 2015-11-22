#pragma once
#include "Transaction.h"

class Memory
{
private:
	int _sizeOfModule;
	int _numberOfModules;

	// Double array for the memory modules. 
	// One is for the memory, the second is for module number.
	char ** _theMemory;
	

public:
	Memory(void);
	~Memory(void);

};

