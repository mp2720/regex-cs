#ifndef REGEX_CS_RUNTIME_COMMON
#define REGEX_CS_RUNTIME_COMMON

#include "api.h"

#define RCS_ARRAY_LEN(arr) (sizeof(arr) / sizeof(*arr))

#if defined(__x86_64__) || defined(__i386__)
// Much more convenient than `raise(SIGTRAP)`.
// GDB just shows you the exact location you placed it and not the libc code.
#define RCS_BREAKPOINT() __asm__("int $3\n")
#endif

// ∅ => can match nothing => ε
//
// ~∅ => matches any char => non-ε
static bool rcs_nfa_state_is_epsilon(const struct rcs_nfa_state *state) {
    return state->ranges_len == 0 && !state->inverted_match;
}

// No outgoing transitions => sink.
// Or check if address is equal to the `nfa.sink`.
static bool rcs_nfa_state_is_sink(const struct rcs_nfa_state *state) {
    return state->next_len == 0;
}

#endif
