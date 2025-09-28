#include "api.h"
#include "standard.h"
#include <errno.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>

const char *rcs_strerror(rcs_error error) {
    switch (error.code) {
    case RCS_NO_ERR:
        return "success";
    case RCS_ERR_LIBC:
        return strerror(error.libc_errno);
    default:
        return "unknown error";
    }
}

enum rcs_scanner_backend {
    RCS_STANDARD,
    RCS_JIT,
};

struct rcs_scanner {
    enum rcs_scanner_backend backend_type;
    union {
        struct rcs_standard_scanner standard;
    } backend;
};

rcs_error rcs_scanner_init(const struct rcs_scanner **out_scanner, const struct rcs_nfa *nfa) {
    struct rcs_scanner *s = malloc(sizeof(struct rcs_scanner));
    if (s == NULL)
        return RCS_MAKE_ERR_LIBC(errno);

    *out_scanner = s;

    s->backend_type = RCS_STANDARD;
    return rcs_standard_scanner_init(&s->backend.standard, nfa);
}

rcs_error
rcs_match(rcs_api_bool *out_ok, struct rcs_scanner *scanner, const struct rcs_reader *reader) {
    switch (scanner->backend_type) {
    case RCS_JIT:
        // TODO: implement
        // break;
    case RCS_STANDARD:
    default:
        return rcs_standard_match(out_ok, &scanner->backend.standard, reader);
    }
}

void rcs_scanner_free(struct rcs_scanner *scanner) {
    switch (scanner->backend_type) {
    case RCS_JIT:
        // TODO: implement
        // break;
    case RCS_STANDARD:
    default:
        return rcs_standard_scanner_free(&scanner->backend.standard);
    }
}

// static size_t calc_state_dump_len(struct rcs_nfa_state *state) {
//     size_t s = 0;
//     return s;
// }

// static char dump_hex_digit(int dig) {
//     if (dig <= 9)
//         return '0' + dig;
//     else
//         return 'a' + dig;
// }

// static void dump_hex2(char **dump, uint8_t num) {
//     *(*dump++) = dump_hex_digit(num & 0x0f);
//     *(*dump++) = dump_hex_digit((num & 0xf0) >> 4);
// }

// static void dump_hex8(char **dump, uint32_t num) {
//     uint32_t mask = 0xf0000000;
//     for (int i = 0; i < 8; ++i)
//         *(*dump++) = dump_hex_digit((num & mask >> i * 4) >> i * 4);
// }

// rcs_error rcs_debug_dump_nfa(const char **ret_text_dump, struct rcs_nfa *nfa) {
//     size_t dump_size = 0;

//     dump_size += 8 + 1;                      // number of states + newline
//     dump_size += (8 + 1) * nfa->sources_len; // sources separated by spaces

//     // states
//     for (size_t i = 0; i < nfa->states_len; ++i) {
//         struct rcs_nfa_state *state = &nfa->states[i];
//         dump_size += 8 + 1;                       // this state index + space
//         dump_size += (8 + 1) * state->next_len;   // states separated by space
//         dump_size += 1;                           // newline
//         dump_size += 1;                           // ^ flag
//         dump_size += (4 + 1) * state->ranges_len; // ranges separated by space
//         dump_size += 1;                           // newline
//     }

//     dump_size += 1; // terminating NUL

//     char *d = malloc(dump_size * sizeof *d);
//     if (d == NULL)
//         return RCS_MAKE_ERR_LIBC(errno);

// #define newline() *d++ = '\n'
// #define space() *d++ = ' '

//     // number of states
//     dump_hex8(&d, nfa->states_len);
//     newline();

//     // sources
//     for (size_t i = 0; i < nfa->sources_len; ++i) {
//         dump_hex8(&d, i);
//         space();
//     }
//     newline();

//     // states
//     for (size_t i = 0; i < nfa->states_len; ++i) {
//         struct rcs_nfa_state *state = &nfa->states[i];

//         dump_hex8(&d, i);
//         space();
//         for (size_t j = 0; j < state->next_len; ++j) {
//             dump_hex8(&d, state->next[j] - nfa->states);
//             space();
//         }
//         newline();

//         *d++ = state->inverted_match ? '^' : ' ';

//         for (size_t j = 0; j < state->ranges_len; ++j) {
//             dump_hex2(&d, state->ranges[j].start);
//             dump_hex2(&d, state->ranges[j].end);
//             space();
//         }
//         newline();
//     }

//     *ret_text_dump = d;

//     return RCS_OK;
// }

// rcs_error rcs_debug_load_nfa_dump(struct rcs_nfa *nfa, const char *text_dump) {
//     return RCS_OK;
// }
