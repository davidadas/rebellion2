#pragma once

#include <cfenv>
#include <cmath>
#include <cstdint>
#include <limits>

#if defined(__i386__) || defined(__x86_64__)
#include <xmmintrin.h>
#define TACTICAL_X86 1
#endif

#if defined(_WIN32)
#define TACTICAL_EXPORT extern "C" __declspec(dllexport)
#else
#define TACTICAL_EXPORT extern "C" __attribute__((visibility("default")))
#endif

#if defined(__GNUC__) || defined(__clang__)
#define TACTICAL_NOINLINE __attribute__((noinline))
#else
#define TACTICAL_NOINLINE __declspec(noinline)
#endif

struct TacticalVector
{
    float x;
    float y;
    float z;
};

static_assert(sizeof(TacticalVector) == 12);
static_assert(std::numeric_limits<float>::digits == 24);
static_assert(std::numeric_limits<double>::digits == 53);

class TacticalArithmeticScope
{
    std::fenv_t previous_environment{};
    bool captured = false;
    bool restored = false;
    bool initialized = false;
#if TACTICAL_X86
    unsigned short previous_control{};
    unsigned previous_simd_control{};
#endif

public:
    TacticalArithmeticScope() noexcept
    {
#if TACTICAL_X86
        asm volatile("fnstcw %0" : "=m"(previous_control) : : "memory");
        previous_simd_control = _mm_getcsr();
        constexpr unsigned precision = std::numeric_limits<long double>::digits > 53 ? 0x0300u : 0x0200u;
        const unsigned short control = static_cast<unsigned short>((previous_control & ~0x0f00u) | 0x003fu | precision);
        if (control != previous_control)
            asm volatile("fldcw %0" : : "m"(control) : "memory");
        const unsigned simd = (previous_simd_control & ~0xe040u) | 0x1f80u;
        if (simd != previous_simd_control)
            _mm_setcsr(simd);
#else
        captured = std::fegetenv(&previous_environment) == 0;
        if (!captured || std::fesetenv(FE_DFL_ENV) != 0)
            return;
#endif
        initialized = true;
    }

    bool ready() const noexcept { return initialized; }

    bool restore() noexcept
    {
        if (restored)
            return true;
        restored = true;
#if TACTICAL_X86
        _mm_setcsr(previous_simd_control);
        asm volatile("fldcw %0" : : "m"(previous_control) : "memory");
        return true;
#else
        return captured && std::fesetenv(&previous_environment) == 0;
#endif
    }

    ~TacticalArithmeticScope() { if (!restored) (void)restore(); }
    TacticalArithmeticScope(const TacticalArithmeticScope &) = delete;
    TacticalArithmeticScope &operator=(const TacticalArithmeticScope &) = delete;
};

template <class Operation>
int tactical_invoke(Operation operation) noexcept
{
    TacticalArithmeticScope environment;
    if (!environment.ready())
        return 0;
    operation();
    return environment.restore() ? 1 : 0;
}

TACTICAL_NOINLINE float tactical_vector_length_body(float x, float y, float z);
