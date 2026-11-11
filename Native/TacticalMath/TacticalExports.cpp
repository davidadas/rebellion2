#include "TacticalMath.h"

void rotate_heading(float, float, float, const TacticalVector &, TacticalVector &, float &);
void normalize_direction(TacticalVector &);
void rotate_battery_direction(const TacticalVector &, std::uint32_t, TacticalVector &);
void direct(std::uint32_t, const TacticalVector *, TacticalVector *);
void surround(std::uint32_t, float, float, float *, TacticalVector *);
void standoff(std::uint32_t, std::uint32_t, float, float, const TacticalVector *, TacticalVector *);
void member_target(const TacticalVector *, const TacticalVector *, const TacticalVector *, TacticalVector *);
void direct_member(const TacticalVector &, const TacticalVector &, const TacticalVector &, const TacticalVector &, TacticalVector &);
void rotate_offsets(float, TacticalVector *, std::uint32_t);
float update_spacing(float, float);

TACTICAL_EXPORT std::uint32_t tactical_math_abi() { return 1; }
TACTICAL_EXPORT std::uint32_t tactical_math_significand_bits() { return std::numeric_limits<long double>::digits; }
TACTICAL_EXPORT std::uint32_t tactical_math_vector_size() { return sizeof(TacticalVector); }

TACTICAL_EXPORT int tactical_math_length(float x, float y, float z, float *result)
{
    return tactical_invoke([&] { *result = tactical_vector_length_body(x, y, z); });
}

TACTICAL_EXPORT int tactical_math_turn(float angle, float elapsed, float rate, const TacticalVector *target, TacticalVector *current, float *applied)
{
    return tactical_invoke([&] { rotate_heading(angle, elapsed, rate, *target, *current, *applied); });
}

TACTICAL_EXPORT int tactical_math_normalize_direction(TacticalVector *direction)
{
    return tactical_invoke([&] { normalize_direction(*direction); });
}

TACTICAL_EXPORT int tactical_math_battery_direction(const TacticalVector *delta, std::uint32_t arc, TacticalVector *result)
{
    return tactical_invoke([&] { rotate_battery_direction(*delta, arc, *result); });
}

TACTICAL_EXPORT int tactical_math_direct_offset(std::uint32_t count, const TacticalVector *heading, TacticalVector *result)
{
    return tactical_invoke([&] { direct(count, heading, result); });
}
TACTICAL_EXPORT int tactical_math_surround_offset(std::uint32_t count, float previous_scale, float configured_scale, float *stored_scale, TacticalVector *result)
{
    return tactical_invoke([&] { surround(count, previous_scale, configured_scale, stored_scale, result); });
}
TACTICAL_EXPORT int tactical_math_line_offset(std::uint32_t count, std::uint32_t maximum_count, float extent, float floor, const TacticalVector *heading, TacticalVector *result)
{
    return tactical_invoke([&] { standoff(count, maximum_count, extent, floor, heading, result); });
}
TACTICAL_EXPORT int tactical_math_member_target(const TacticalVector *marker, const TacticalVector *offset, const TacticalVector *unit, const TacticalVector *velocity, std::uint32_t direct_mode, TacticalVector *result)
{
    return tactical_invoke([&] {
        if (direct_mode != 0)
            direct_member(*marker, *offset, *unit, *velocity, *result);
        else
            member_target(marker, offset, unit, result);
    });
}
TACTICAL_EXPORT int tactical_math_rotate_offsets(float angle, TacticalVector *values, std::uint32_t count)
{
    return tactical_invoke([&] { rotate_offsets(angle, values, count); });
}
TACTICAL_EXPORT int tactical_math_spacing(float floor, float extent, float *result)
{
    return tactical_invoke([&] { *result = update_spacing(floor, extent); });
}
