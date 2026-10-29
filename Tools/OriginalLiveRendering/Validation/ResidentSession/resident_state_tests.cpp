#include "resident_session.hpp"
#include <cstdio>
#include <cstdlib>
using namespace resident_session;
unsigned checks{};
void check(bool value) { ++checks; if (!value) std::abort(); }
int main() {
    const Identity owner{42, 1234}; const std::string hash(64, 'a'), job(32, '1');
    const Cleanup good{true,true,true,true,true,true,true,true,true};
    check(Session({0,1}).phase() == Phase::blocked);
    check(Session({1,0}).phase() == Phase::blocked);
    check(!same(owner,{42,1235})); check(!same(owner,{43,1234}));
    check(!hex(std::string(32,'A'),32)); check(!hex(std::string(31,'a'),32));
    check(!hex(std::string(32,'g'),32));
    Session s(owner);
    check(!s.open({43,1234},job,hash)); check(!s.open({42,1235},job,hash));
    check(!s.open(owner,job,"bad")); check(s.count()==0);
    check(!s.load(owner)); check(!s.exited(owner,true));
    check(!s.abandon_before_load(owner));
    check(s.open(owner,job,hash)); check(!s.open(owner,std::string(32,'2'),hash));
    check(!s.stop()); check(!s.load({42,1235})); check(s.load(owner));
    check(!s.abandon_before_load(owner)); check(!s.exited(owner,false));
    check(!s.exited({42,1235},true)); check(s.exited(owner,true));
    check(s.retire(owner,good)); check(!s.open(owner,job,hash));
    check(s.open(owner,std::string(32,'2'),hash)); check(s.abandon_before_load(owner));
    check(s.stop()); check(!s.open(owner,std::string(32,'3'),hash));
    for (unsigned missing=0;missing<9;++missing) {
        Session candidate(owner); check(candidate.open(owner,job,hash));
        check(candidate.load(owner)); check(candidate.exited(owner,true));
        auto bad=good;
        bool* fields[]={&bad.worker_signaled,&bad.handle_identity,&bad.worker_handle_closed,
          &bad.registry_closed,&bad.hooks_disabled,&bad.drained,&bad.code_restored,
          &bad.resources_released,&bad.library_retired};
        *fields[missing]=false;
        check(!candidate.retire(owner,bad)); check(candidate.phase()==Phase::blocked);
        check(!candidate.open(owner,std::string(32,'2'),hash)); check(!candidate.stop());
    }
    Session capped(owner);
    for(unsigned i=0;i<maximum_jobs;++i) {
        check(capped.open(owner,std::string(32,char('0'+i)),hash));
        check(capped.load(owner)); check(capped.exited(owner,true)); check(capped.retire(owner,good));
    }
    check(capped.count()==maximum_jobs);
    check(!capped.open(owner,std::string(32,'8'),hash));
    Session foreign(owner);check(foreign.open(owner,job,hash));check(foreign.load(owner));
    check(foreign.exited(owner,true));check(!foreign.retire({43,1234},good));
    Session blocked(owner);blocked.block();check(!blocked.open(owner,job,hash));
    std::printf("PASS %u resident lifecycle checks; TestOnly\n",checks);
}
