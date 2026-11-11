#include "TacticalMath.h"

TACTICAL_NOINLINE float tactical_vector_length_body(float first, float second, float third)
{
    const long double x = first;
    const long double y = second;
    const long double z = third;
    return static_cast<float>(std::sqrt(x * x + (y * y + z * z)));
}

TACTICAL_NOINLINE void rotate_heading(float turn_angle, float elapsed, float turn_rate, const TacticalVector &target, TacticalVector &current, float &applied)
{
    long double signed_angle = turn_angle;
    const long double side = -static_cast<long double>(current.z) * target.x + static_cast<long double>(target.z) * current.x + static_cast<long double>(target.y) * current.y;
    if (side < 0.0L || std::isunordered(side, 0.0L))
        signed_angle = -signed_angle;
    const long double ratio = (static_cast<long double>(turn_rate) * elapsed) / (static_cast<long double>(turn_angle) * 1000.0L);
    if (!(ratio < 1.0L || std::isunordered(ratio, 1.0L)))
    {
        current = target;
        applied = static_cast<float>(signed_angle);
        return;
    }
    const long double turn = ratio * signed_angle;
    const float cosine = static_cast<float>(std::cos(turn));
    const float sine = static_cast<float>(std::sin(turn));
    const float old_x = current.x;
    const float old_z = current.z;
    current.z = static_cast<float>(static_cast<long double>(old_x) * sine + static_cast<long double>(cosine) * old_z);
    current.x = static_cast<float>(static_cast<long double>(old_x) * cosine - static_cast<long double>(sine) * old_z);
    const long double x = current.x;
    const long double y = current.y;
    const long double z = current.z;
    const float magnitude = static_cast<float>(std::sqrt(x * x + z * z + y * y));
    if (magnitude > 0.0f)
    {
        const long double inverse = 1.0L / magnitude;
        current.x = static_cast<float>(current.x * inverse);
        current.y = static_cast<float>(current.y * inverse);
        current.z = static_cast<float>(current.z * inverse);
    }
    applied = static_cast<float>(turn);
}

TACTICAL_NOINLINE void normalize_direction(TacticalVector &value)
{
    const long double x = value.x;
    const long double y = value.y;
    const long double z = value.z;
    const float length = static_cast<float>(std::sqrt(x * x + z * z + y * y));
    if (length > 0.0f)
    {
        const long double inverse = 1.0L / length;
        value.x = static_cast<float>(inverse * value.x);
        value.y = static_cast<float>(inverse * value.y);
        value.z = static_cast<float>(inverse * value.z);
    }
}

TACTICAL_NOINLINE void rotate_battery_direction(const TacticalVector &delta, std::uint32_t arc, TacticalVector &result)
{
    if (arc == 4)
        arc = 0;
    constexpr float pi = 3.1415927f;
    long double angle = static_cast<long double>(arc) * static_cast<long double>(pi) * static_cast<long double>(0.5f);
    const long double full_turn = static_cast<long double>(pi) + static_cast<long double>(pi);
    if (angle >= full_turn)
        angle -= full_turn;
    const long double cosine = std::cos(angle);
    const long double sine = std::sin(angle);
    const float stored_cosine = static_cast<float>(cosine);
    result = {
        static_cast<float>(static_cast<long double>(delta.x) * static_cast<long double>(stored_cosine) - sine * static_cast<long double>(delta.z)),
        delta.y,
        static_cast<float>(static_cast<long double>(delta.x) * sine + cosine * static_cast<long double>(delta.z))};
}
