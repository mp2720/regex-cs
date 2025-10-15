#ifndef REGEX_CS_RUNTIME_COMMON
#define REGEX_CS_RUNTIME_COMMON

#include "api.h"

#define RCS_ARRAY_LEN(arr) (sizeof(arr) / sizeof(*arr))

#define RCS_DIV_CEILING(x, y) (((x) + (y) - 1) / (y))

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

// No outgoing transitions => accept.
// Or check if address is equal to the `nfa.accept`.
static bool rcs_nfa_state_is_accept(const struct rcs_nfa_state *state) {
    return state->next_len == 0;
}

#if defined(__clang__) || defined(__GNUC__)
#define RCS_NODISCARD __attribute__((__warn_unused_result__))
#define RCS_NORETURN __attribute__((noreturn))
#else
#define RCS_NODISCARD
#endif

#endif
