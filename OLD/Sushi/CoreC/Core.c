#include "Core.h"

int32_t __SafeAdd32(int32_t a, int32_t b)
{
    return (int32_t)((uint32_t)a + (uint32_t)b);
}

int32_t __SafeSubtract32(int32_t a, int32_t b)
{
    return (int32_t)((uint32_t)a - (uint32_t)b);
}

int32_t __SafeMultiply32(int32_t a, int32_t b)
{
    return (int32_t)((uint32_t)a * (uint32_t)b);
}

int32_t __SafeDivide32(int32_t a, int32_t b)
{
    return (int32_t)((uint32_t)a / (uint32_t)b);
}