#ifndef REGEX_CS_RUNTIME_JIT
#define REGEX_CS_RUNTIME_JIT

#include "common.h"
#include "stdbool.h"

struct rcs_jit_scanner;

#ifdef __x86_64__
#include "amd64/jit_impl.h" // defines struct rcs_jit_scanner
#else
struct rcs_jit_scanner { // stub
    char _;
};
#endif

// Returns false if the given NFA doesn't fit the requirements.
// True if the scanner was initialized or an error occurred (`err` is set).
RCS_NODISCARD
bool rcs_jit_scanner_init(
    rcs_error *err,
    struct rcs_jit_scanner *scanner,
    const struct rcs_nfa *nfa
);

RCS_NODISCARD
rcs_error rcs_jit_match(
    rcs_api_bool *out_ok,
    struct rcs_jit_scanner *scanner,
    const struct rcs_reader *reader
);

// Does not free the scanner struct itself, only its inner resources.
void rcs_jit_scanner_free(struct rcs_jit_scanner *scanner);

#endif
