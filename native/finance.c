#include <stdint.h>

int32_t savings_rate(int64_t income_minor, int64_t savings_minor) {
    if (income_minor <= 0 || income_minor > 9000000000000000LL) return -1;
    if (savings_minor < 0 || savings_minor > income_minor) return -1;
    return (int32_t)((savings_minor * 100) / income_minor);
}
