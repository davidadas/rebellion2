#include "../TacticalMath.h"
#include <bit>
#include <cstdio>
#include <initializer_list>

extern "C" std::uint32_t tactical_math_abi();
extern "C" std::uint32_t tactical_math_significand_bits();
extern "C" std::uint32_t tactical_math_vector_size();
extern "C" int tactical_math_length(float, float, float, float *);
extern "C" int tactical_math_turn(float, float, float, const TacticalVector *, TacticalVector *, float *);
extern "C" int tactical_math_member_target(const TacticalVector *, const TacticalVector *, const TacticalVector *, const TacticalVector *, std::uint32_t, TacticalVector *);
extern "C" int tactical_math_direct_offset(std::uint32_t, const TacticalVector *, TacticalVector *);
extern "C" int tactical_math_line_offset(std::uint32_t, std::uint32_t, float, float, const TacticalVector *, TacticalVector *);
extern "C" int tactical_math_spacing(float, float, float *);
extern "C" int tactical_math_rotate_offsets(float, TacticalVector *, std::uint32_t);
extern "C" int tactical_math_battery_direction(const TacticalVector *, std::uint32_t, TacticalVector *);

static int failures = 0;
static std::uint64_t comparisons = 0;

static void require(bool condition, const char *name)
{
    ++comparisons;
    if (!condition)
    {
        ++failures;
        if (failures < 12)
            std::fprintf(stderr, "Failed: %s\n", name);
    }
}

static TACTICAL_NOINLINE float reference_length(float x, float y, float z)
{
    long double first = x;
    long double inner = static_cast<long double>(y) * y + static_cast<long double>(z) * z;
    return static_cast<float>(std::sqrt(first * first + inner));
}

static void length_contracts()
{
    TacticalArithmeticScope environment;
    require(environment.ready(), "reference environment");
    std::uint32_t seed = 42;
    for (unsigned i = 0; i < 250000; ++i)
    {
        float values[3];
        for (float &value : values)
        {
            seed = seed * 1664525u + 1013904223u;
            value = std::bit_cast<float>(seed);
        }
        const float expected = reference_length(values[0], values[1], values[2]);
        float actual;
        require(tactical_math_length(values[0], values[1], values[2], &actual) != 0, "length status");
        require(std::isnan(expected) ? std::isnan(actual) : std::bit_cast<std::uint32_t>(expected) == std::bit_cast<std::uint32_t>(actual), "target length expression");
    }
    float result;
    require(tactical_math_length(0.9999999403953552f, 0.00048828125f, 0x1p-30f, &result) != 0, "midpoint status");
    const auto expected_midpoint = std::numeric_limits<long double>::digits == 53 ? 0x3f800000u : 0x3f800001u;
    if (std::numeric_limits<long double>::digits == 53 || std::numeric_limits<long double>::digits == 64)
        require(std::bit_cast<std::uint32_t>(result) == expected_midpoint, "target midpoint");
    require(environment.restore(), "reference restoration");
}

static void formation_contracts()
{
    TacticalVector heading{0, 0, 1}, result{}, marker{100000000, 0, 0}, offset{12, 0, 0}, unit{100000000, 0, 0}, velocity{};
    require(tactical_math_member_target(&marker, &offset, &unit, &velocity, 0, &result) != 0, "member status");
    require(result.x == -12 && result.y == 0 && result.z == 0, "retained displacement");
    require(tactical_math_direct_offset(1, &heading, &result) != 0 && result.x == 0 && result.y == 0 && result.z == 0, "first direct offset");
    require(tactical_math_line_offset(2, 2, 3.6f, 12, &heading, &result) != 0 && result.x == 12, "line offset");
    float spacing;
    require(tactical_math_spacing(std::numeric_limits<float>::quiet_NaN(), 10, &spacing) != 0 && spacing == 12, "unordered previous spacing");
    TacticalVector values[2]{{1, 2, 3}, {-4, 5, 6}};
    require(tactical_math_rotate_offsets(0, values, 2) != 0 && values[0].x == 1 && values[0].y == 2 && values[1].z == 6, "batched zero rotation");
}

static void environment_contracts()
{
    std::fenv_t saved;
    require(std::fegetenv(&saved) == 0, "portable environment capture");
    for (int rounding : {FE_TONEAREST, FE_DOWNWARD, FE_UPWARD, FE_TOWARDZERO})
    {
        require(std::fesetround(rounding) == 0, "portable caller rounding");
        float length;
        require(tactical_math_length(3, 4, 0, &length) != 0 && length == 5, "portable native rounding");
        require(std::fegetround() == rounding, "portable rounding restoration");
        const float minimum = std::bit_cast<float>(1u);
        require(tactical_math_length(minimum, 0, 0, &length) != 0 && std::bit_cast<std::uint32_t>(length) == 1u, "portable gradual underflow");
    }
    require(std::fesetenv(&saved) == 0, "portable environment restoration");
#if TACTICAL_X86
    unsigned short initial;
    asm volatile("fnstcw %0" : "=m"(initial));
    const unsigned initial_simd = _mm_getcsr();
    const TacticalVector target{-0.14254497f, 0, -0.98978835f};
    const TacticalVector start{0.7094057f, 0, 0.7048003f};
    TacticalVector expected = start;
    float expected_angle;
    const float angle = 2.495132f;
    require(tactical_math_turn(angle, 625, 0.5235988f, &target, &expected, &expected_angle) != 0, "baseline turn");
    for (unsigned precision : {0x0200u, 0x0300u})
    for (unsigned rounding : {0u, 0x0400u, 0x0800u, 0x0c00u})
    for (unsigned denormal : {0u, 0x40u, 0x8000u, 0x8040u})
    {
        const unsigned short incoming = static_cast<unsigned short>((initial & ~0x0f00u) | precision | rounding);
        const unsigned simd = (initial_simd & ~0xe040u) | (rounding << 3) | denormal;
        asm volatile("fldcw %0" : : "m"(incoming) : "memory");
        _mm_setcsr(simd);
        TacticalVector actual = start;
        float applied;
        require(tactical_math_turn(angle, 625, 0.5235988f, &target, &actual, &applied) != 0, "turn environment status");
        unsigned short restored;
        asm volatile("fnstcw %0" : "=m"(restored));
        const unsigned restored_simd = _mm_getcsr();
        require(restored == incoming && restored_simd == simd, "caller controls restored");
        require(std::bit_cast<std::uint32_t>(actual.x) == std::bit_cast<std::uint32_t>(expected.x) && std::bit_cast<std::uint32_t>(applied) == std::bit_cast<std::uint32_t>(expected_angle), "turn independent of caller controls");
        const float subnormal = std::bit_cast<float>(1u);
        float length;
        require(tactical_math_length(subnormal, 0, 0, &length) != 0 && std::bit_cast<std::uint32_t>(length) == 1u, "gradual float underflow");
    }
    _mm_setcsr(initial_simd);
    asm volatile("fldcw %0" : : "m"(initial) : "memory");
#endif
#if defined(__aarch64__) || defined(__arm64__)
    std::uint64_t original;
    asm volatile("mrs %0, fpcr" : "=r"(original));
    for (std::uint64_t mode : {0ull, 0x01000000ull, 0x02000000ull, 0x03000000ull})
    {
        const std::uint64_t incoming = (original & ~0x03000000ull) | mode;
        asm volatile("msr fpcr, %0" : : "r"(incoming) : "memory");
        float length;
        const float minimum = std::bit_cast<float>(1u);
        require(tactical_math_length(minimum, 0, 0, &length) != 0 && std::bit_cast<std::uint32_t>(length) == 1u, "ARM gradual underflow");
        std::uint64_t restored;
        asm volatile("mrs %0, fpcr" : "=r"(restored));
        require(restored == incoming, "ARM control restoration");
    }
    asm volatile("msr fpcr, %0" : : "r"(original) : "memory");
#endif
}

static TACTICAL_NOINLINE TacticalVector reference_battery_direction(TacticalVector delta, std::uint32_t arc)
{
    if (arc == 4)
        arc = 0;
    constexpr float pi = 3.1415927f;
    long double angle = static_cast<long double>(arc) * pi * 0.5f;
    const long double full_turn = static_cast<long double>(pi) + pi;
    if (angle >= full_turn)
        angle -= full_turn;
    const long double cosine = std::cos(angle);
    const long double sine = std::sin(angle);
    const float stored_cosine = static_cast<float>(cosine);
    return {
        static_cast<float>(static_cast<long double>(delta.x) * stored_cosine - sine * delta.z),
        delta.y,
        static_cast<float>(static_cast<long double>(delta.x) * sine + cosine * delta.z)};
}

static void battery_contracts()
{
    TacticalArithmeticScope environment;
    require(environment.ready(), "battery reference environment");
    std::uint32_t seed = 42;
    for (unsigned index = 0; index < 25000; ++index)
    {
        TacticalVector delta;
        for (float *value : {&delta.x, &delta.y, &delta.z})
        {
            seed = seed * 1664525u + 1013904223u;
            *value = std::bit_cast<float>(seed);
        }
        const auto arc = index % 5;
        const auto expected = reference_battery_direction(delta, arc);
        TacticalVector actual;
        require(tactical_math_battery_direction(&delta, arc, &actual) != 0, "battery status");
        for (unsigned component = 0; component < 3; ++component)
        {
            const float first = component == 0 ? expected.x : component == 1 ? expected.y : expected.z;
            const float second = component == 0 ? actual.x : component == 1 ? actual.y : actual.z;
            require(std::isnan(first) ? std::isnan(second) : std::bit_cast<std::uint32_t>(first) == std::bit_cast<std::uint32_t>(second), "target battery expression");
        }
    }
    TacticalVector delta{3, 4, 5}, actual;
    require(tactical_math_battery_direction(&delta, 4, &actual) != 0 && actual.x == 3 && actual.y == 4 && actual.z == 5, "empty battery sentinel");
    delta = {std::numeric_limits<float>::infinity(), 0, 0};
    require(tactical_math_battery_direction(&delta, 0, &actual) != 0 && std::isinf(actual.x) && std::isnan(actual.z), "unordered zero product");
    require(environment.restore(), "battery reference restoration");
}

int main()
{
    require(tactical_math_abi() == 1, "ABI");
    require(tactical_math_vector_size() == sizeof(TacticalVector), "vector layout");
    require(tactical_math_significand_bits() == std::numeric_limits<long double>::digits, "target precision");
    length_contracts();
    formation_contracts();
    battery_contracts();
    environment_contracts();
    std::printf("Precision %u; comparisons %llu; failures %d\n", tactical_math_significand_bits(), static_cast<unsigned long long>(comparisons), failures);
    return failures == 0 ? 0 : 1;
}
