#include <windows.h>
#include "resident_session.hpp"
#include <cstdio>
#include <cstdlib>
using namespace resident_session;
unsigned checks{};
void check(bool value) { ++checks; if(!value)std::abort(); }
std::uint64_t birth() {
 FILETIME c{},e{},k{},u{};check(GetProcessTimes(GetCurrentProcess(),&c,&e,&k,&u)!=FALSE);
 return (std::uint64_t(c.dwHighDateTime)<<32)|c.dwLowDateTime;
}
int wmain(int argc,wchar_t**argv) {
 check(argc==3);const Identity original{GetCurrentProcessId(),birth()};Session session(original);
 DWORD tid[2]{};
 for(unsigned index=0;index<2;++index) {
  check(session.open({GetCurrentProcessId(),birth()},std::string(32,char('1'+index)),std::string(64,'a')));
  check(session.load(original));HMODULE module=LoadLibraryW(argv[index+1]);check(module!=nullptr);
  const auto acquire=reinterpret_cast<HANDLE(*)()>(GetProcAddress(module,"Acquire"));
  const auto retire=reinterpret_cast<DWORD(*)()>(GetProcAddress(module,"Retire"));
  check(acquire&&retire);HANDLE exact=acquire();check(exact!=nullptr);
  check(GetProcessIdOfThread(exact)==original.pid);tid[index]=GetThreadId(exact);check(tid[index]!=0);
  check(WaitForSingleObject(exact,5000)==WAIT_OBJECT_0);
  check(session.exited({GetCurrentProcessId(),birth()},true));check(CloseHandle(exact)!=FALSE);
  const auto flags=retire();check(flags==31);
  check(session.retire(original,{true,true,true,true,(flags&1)!=0,(flags&2)!=0,(flags&4)!=0,(flags&8)!=0,(flags&16)!=0}));
  // Modules remain mapped: no FreeLibrary, process restart, UI, or game input.
  check(GetModuleHandleW(argv[index+1])==module);
 }
 check(session.count()==2);check(same(original,{GetCurrentProcessId(),birth()}));
 check(session.stop());
 std::printf("PASS %u Win32 resident host checks; captures=2 pid=%lu birth=%llu restarts=0 TestOnly\n",checks,original.pid,static_cast<unsigned long long>(original.birth));
}
