#include "TacticalMath.h"

static std::int32_t truncate_low_word(long double value)
{
    if (!std::isfinite(value) || value < -9223372036854775808.0L || value >= 9223372036854775808.0L)
        return 0;
    const std::int64_t truncated = static_cast<std::int64_t>(value);
    return static_cast<std::int32_t>(static_cast<std::uint32_t>(static_cast<std::uint64_t>(truncated)));
}

TACTICAL_NOINLINE void direct(std::uint32_t count, const TacticalVector *heading, TacticalVector *result)
{
    if (count == 1)
    {
        *result = {0.0f, 0.0f, 0.0f};
        return;
    }
    const std::int32_t side = (count & 1u) != 0u ? 1 : -1;
    const std::int32_t spacing = truncate_low_word(static_cast<long double>(side) * 3.0L * -1.5L);
    const std::int32_t half_count = static_cast<std::int32_t>(count) >> 1;
    const std::int32_t product = static_cast<std::int32_t>(static_cast<std::uint32_t>(spacing) * static_cast<std::uint32_t>(half_count));
    const std::int32_t angle_index = static_cast<std::int32_t>(1u - static_cast<std::uint32_t>(product));
    const long double base_angle = static_cast<long double>(angle_index) * static_cast<long double>(3.1415927f);
    const long double angle_a = base_angle * static_cast<long double>(0.6666667f);
    const long double cosine_a = std::cos(angle_a);
    const float sine_a = static_cast<float>(std::sin(static_cast<long double>(static_cast<float>(angle_a))));
    const long double angle_b = base_angle * static_cast<long double>(0.5f);
    const long double cosine_b = std::cos(angle_b);
    const long double sine_b_extended = std::sin(static_cast<long double>(static_cast<float>(angle_b)));
    const float sine_b = static_cast<float>(sine_b_extended);
    const long double angle_c = base_angle * static_cast<long double>(0.4f);
    const long double cosine_c = std::cos(angle_c);
    const float sine_c = static_cast<float>(std::sin(static_cast<long double>(static_cast<float>(angle_c))));
    const float rotated_zx = static_cast<float>(cosine_a * heading->z + static_cast<long double>(sine_a) * heading->x);
    const float rotated_xz = static_cast<float>(static_cast<long double>(static_cast<float>(cosine_a)) * heading->x - static_cast<long double>(sine_a) * heading->z);
    const float rotated_yz = static_cast<float>(sine_b_extended * rotated_zx + cosine_b * heading->y);
    const float cosine_b_rounded = static_cast<float>(cosine_b);
    const float cosine_c_rounded = static_cast<float>(cosine_c);
    *result = {
        static_cast<float>(static_cast<long double>(cosine_c_rounded) * rotated_yz + static_cast<long double>(sine_c) * rotated_xz),
        static_cast<float>(static_cast<long double>(cosine_c_rounded) * rotated_xz - static_cast<long double>(sine_c) * rotated_yz),
        static_cast<float>(static_cast<long double>(cosine_b_rounded) * rotated_zx - static_cast<long double>(sine_b) * heading->y)};
}

TACTICAL_NOINLINE void surround(std::uint32_t count, float previous_scale, float configured_scale, float *stored_scale, TacticalVector *result)
{
    const float diagonal_three = static_cast<float>(static_cast<long double>(previous_scale) / std::sqrt(3.0L));
    const float diagonal_two = static_cast<float>(static_cast<long double>(previous_scale) / std::sqrt(2.0L));
    TacticalVector direction;
    switch (count % 22u)
    {
    case 0: direction = {diagonal_two, -diagonal_two, 0.0f}; break;
    case 1: direction = {0.0f, 0.0f, previous_scale}; break;
    case 2: direction = {0.0f, 0.0f, -previous_scale}; break;
    case 3: direction = {previous_scale, 0.0f, 0.0f}; break;
    case 4: direction = {-previous_scale, 0.0f, 0.0f}; break;
    case 5: direction = {0.0f, previous_scale, 0.0f}; break;
    case 6: direction = {0.0f, -previous_scale, 0.0f}; break;
    case 7: direction = {diagonal_three, diagonal_three, diagonal_three}; break;
    case 8: direction = {-diagonal_three, -diagonal_three, -diagonal_three}; break;
    case 9: direction = {-diagonal_three, diagonal_three, diagonal_three}; break;
    case 10: direction = {diagonal_three, -diagonal_three, -diagonal_three}; break;
    case 11: direction = {diagonal_three, -diagonal_three, diagonal_three}; break;
    case 12: direction = {-diagonal_three, diagonal_three, -diagonal_three}; break;
    case 13: direction = {-diagonal_three, -diagonal_three, diagonal_three}; break;
    case 14: direction = {diagonal_three, diagonal_three, -diagonal_three}; break;
    case 15:
    case 19: direction = {0.0f, -diagonal_two, -diagonal_two}; break;
    case 16: direction = {0.0f, -diagonal_two, diagonal_two}; break;
    case 17: direction = {-diagonal_two, diagonal_two, 0.0f}; break;
    case 18: direction = {diagonal_two, diagonal_two, 0.0f}; break;
    case 20: direction = {0.0f, diagonal_two, diagonal_two}; break;
    case 21: direction = {-diagonal_two, -diagonal_two, 0.0f}; break;
    }
    const long double x = direction.x;
    const long double y = direction.y;
    const long double z = direction.z;
    const float length = static_cast<float>(std::sqrt(x * x + (y * y + z * z)));
    if (length > 0.0f)
    {
        const long double inverse = 1.0L / static_cast<long double>(length);
        direction.x = static_cast<float>(inverse * direction.x);
        direction.y = static_cast<float>(inverse * direction.y);
        direction.z = static_cast<float>(inverse * direction.z);
    }
    const long double distance = (static_cast<long double>(count / 22u) * 0.25L + 1.0L) * configured_scale;
    *stored_scale = static_cast<float>(distance);
    *result = {static_cast<float>(distance * direction.x), static_cast<float>(distance * direction.y), static_cast<float>(distance * direction.z)};
}

TACTICAL_NOINLINE void standoff(std::uint32_t count, std::uint32_t maximum_count, float collision_extent, float spacing_floor, const TacticalVector *heading, TacticalVector *result)
{
    if (count == 1)
    {
        *result = {0.0f, 0.0f, 0.0f};
        return;
    }
    std::uint32_t spacing_count = maximum_count;
    if (spacing_count <= count)
        spacing_count = count;
    long double spacing = static_cast<long double>(collision_extent) * static_cast<long double>(1.2f);
    if (static_cast<long double>(spacing_floor) > spacing)
        spacing = static_cast<long double>(spacing_floor);
    const std::int32_t side = (count & 1u) != 0u ? 1 : -1;
    const std::int32_t integer_spacing = truncate_low_word(static_cast<long double>(side) * spacing);
    const std::int32_t half_count = static_cast<std::int32_t>(spacing_count) >> 1;
    const std::int32_t scaled = static_cast<std::int32_t>(static_cast<std::uint32_t>(integer_spacing) * static_cast<std::uint32_t>(half_count));
    *result = {static_cast<float>(-static_cast<long double>(heading->z) * scaled), 0.0f, static_cast<float>(static_cast<long double>(heading->x) * scaled)};
}

TACTICAL_NOINLINE void member_target(const TacticalVector *marker, const TacticalVector *offset, const TacticalVector *unit, TacticalVector *result)
{
    const long double raw_x = static_cast<long double>(marker->x) - offset->x - unit->x;
    const long double raw_y = static_cast<long double>(marker->y) - offset->y - unit->y;
    const long double raw_z = static_cast<long double>(marker->z) - offset->z - unit->z;
    const float x = static_cast<float>(raw_x);
    const float z = static_cast<float>(raw_z);
    const long double squared = static_cast<long double>(x) * x + static_cast<long double>(z) * z + raw_y * raw_y;
    *result = !(squared > 1.0L) ? TacticalVector{0.0f, 0.0f, 0.0f} : TacticalVector{x, static_cast<float>(raw_y), z};
}

TACTICAL_NOINLINE void direct_member(const TacticalVector &marker, const TacticalVector &offset, const TacticalVector &unit, const TacticalVector &velocity, TacticalVector &result)
{
    result = {
        static_cast<float>(static_cast<long double>(marker.x) - offset.x - unit.x),
        static_cast<float>(static_cast<long double>(marker.y) - offset.y - unit.y),
        static_cast<float>(static_cast<long double>(marker.z) - offset.z - unit.z)};
    const long double squared = static_cast<long double>(result.x) * result.x + static_cast<long double>(result.y) * result.y + static_cast<long double>(result.z) * result.z;
    if (!(squared > 4.0L))
        result = velocity;
}

TACTICAL_NOINLINE void rotate_offsets(float angle, TacticalVector *values, std::uint32_t count)
{
    const float cosine = static_cast<float>(std::cos(static_cast<long double>(angle)));
    const float sine = static_cast<float>(std::sin(static_cast<long double>(angle)));
    for (std::uint32_t i = 0; i < count; ++i)
    {
        const float old_x = values[i].x;
        const float old_z = values[i].z;
        values[i].z = static_cast<float>(static_cast<long double>(sine) * old_x + static_cast<long double>(cosine) * old_z);
        values[i].x = static_cast<float>(static_cast<long double>(cosine) * old_x - static_cast<long double>(sine) * old_z);
    }
}

TACTICAL_NOINLINE float update_spacing(float floor, float extent)
{
    long double spacing = static_cast<long double>(extent) * static_cast<long double>(1.2f);
    if (static_cast<long double>(floor) > spacing)
        spacing = static_cast<long double>(floor);
    return static_cast<float>(spacing);
}
