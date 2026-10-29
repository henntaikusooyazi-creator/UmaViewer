#pragma once
#include <array>
#include <cstdint>
#include <string>

// Transport lifetime is independent of capture acceptance. A Pending capture
// can be retired only with separate, positive native cleanup evidence.
namespace resident_session {
inline constexpr unsigned maximum_jobs = 8;
enum class Phase { idle, connected, loaded, exited, retired, blocked, stopped };
struct Identity { std::uint32_t pid{}; std::uint64_t birth{}; };
inline bool same(Identity a, Identity b) noexcept {
    return a.pid && a.birth && a.pid == b.pid && a.birth == b.birth;
}
inline bool hex(const std::string& value, std::size_t size) noexcept {
    if (value.size() != size) return false;
    for (char c : value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
    return true;
}
struct Cleanup {
    bool worker_signaled{}, handle_identity{}, worker_handle_closed{}, registry_closed{};
    bool hooks_disabled{}, drained{}, code_restored{}, resources_released{}, library_retired{};
    bool complete() const noexcept {
        return worker_signaled && handle_identity && worker_handle_closed && registry_closed &&
            hooks_disabled && drained && code_restored && resources_released && library_retired;
    }
};
class Session {
    Identity owner_{};
    Phase phase_{Phase::idle};
    std::array<std::string, maximum_jobs> used_{};
    unsigned count_{};
public:
    explicit Session(Identity owner) : owner_(owner) {
        if (!owner.pid || !owner.birth) phase_ = Phase::blocked;
    }
    Phase phase() const noexcept { return phase_; }
    unsigned count() const noexcept { return count_; }
    bool open(Identity owner, const std::string& job, const std::string& binary_hash) {
        if (!same(owner_, owner) || (phase_ != Phase::idle && phase_ != Phase::retired) ||
            count_ == maximum_jobs || !hex(job, 32) || !hex(binary_hash, 64)) return false;
        for (unsigned i = 0; i < count_; ++i) if (used_[i] == job) return false;
        used_[count_++] = job;
        phase_ = Phase::connected;
        return true;
    }
    bool load(Identity owner) noexcept {
        if (phase_ != Phase::connected || !same(owner_, owner)) return false;
        phase_ = Phase::loaded;
        return true;
    }
    bool exited(Identity owner, bool exact_worker_signaled) noexcept {
        if (phase_ != Phase::loaded || !same(owner_, owner) || !exact_worker_signaled) return false;
        phase_ = Phase::exited;
        return true;
    }
    bool retire(Identity owner, const Cleanup& cleanup) noexcept {
        if (phase_ != Phase::exited || !same(owner_, owner) || !cleanup.complete()) {
            phase_ = Phase::blocked;
            return false;
        }
        phase_ = Phase::retired;
        return true;
    }
    bool abandon_before_load(Identity owner) noexcept {
        if (phase_ != Phase::connected || !same(owner_, owner)) return false;
        phase_ = Phase::retired;
        return true;
    }
    void block() noexcept { phase_ = Phase::blocked; }
    bool stop() noexcept {
        if (phase_ != Phase::idle && phase_ != Phase::retired) return false;
        phase_ = Phase::stopped;
        return true;
    }
};
}
