#include <windows.h>
static HANDLE worker{};
static DWORD WINAPI capture(void*) { Sleep(20); return 0; }
extern "C" __declspec(dllexport) HANDLE Acquire() {
 if(worker)return nullptr;
 worker=CreateThread(nullptr,0,capture,nullptr,0,nullptr);
 HANDLE copy{};
 if(worker&&!DuplicateHandle(GetCurrentProcess(),worker,GetCurrentProcess(),&copy,
     SYNCHRONIZE|THREAD_QUERY_LIMITED_INFORMATION,FALSE,0))return nullptr;
 return copy;
}
extern "C" __declspec(dllexport) DWORD Retire() {
 if(!worker||WaitForSingleObject(worker,0)!=WAIT_OBJECT_0)return 0;
 if(!CloseHandle(worker))return 0;worker=nullptr;
 return 31;
}
BOOL WINAPI DllMain(HINSTANCE,DWORD,LPVOID) { return TRUE; }
