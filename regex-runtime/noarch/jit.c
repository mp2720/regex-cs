#include "../jit.h"
#include "../api.h"
#include "../common.h"

#include <assert.h>
#include <stdbool.h>

RCS_NODISCARD
bool rcs_jit_scanner_init(
    rcs_error *err,
    struct rcs_jit_scanner *scanner,
    const struct rcs_nfa *nfa
) {
    return false;
}

RCS_NODISCARD
rcs_error rcs_jit_match(
    rcs_api_bool *out_ok,
    struct rcs_jit_scanner *scanner,
    const struct rcs_reader *reader
) {
    assert(0 && "not implemented");
}

void rcs_jit_scanner_free(struct rcs_jit_scanner *scanner) {
    assert(0 && "not implemented");
}
