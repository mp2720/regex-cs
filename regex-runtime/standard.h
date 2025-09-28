#ifndef REGEX_CS_RUNTIME_STANDARD
#define REGEX_CS_RUNTIME_STANDARD

#include "api.h"
#include "bitmap.h"
#include <stddef.h>
#include <stdint.h>

struct rcs_standard_scanner {
    const struct rcs_nfa *nfa;

    // 0 is the current, 1 is the next
    // swap on each wave
    rcs_bitmap_word *states_bm[2];
    size_t states_bm_len;

    size_t input_buf_len;
    size_t input_buf_index;
};

rcs_error
rcs_standard_scanner_init(struct rcs_standard_scanner *scanner, const struct rcs_nfa *nfa);

rcs_error rcs_standard_match(
    rcs_api_bool *out_ok,
    struct rcs_standard_scanner *scanner,
    const struct rcs_reader *reader
);

// Does not free the scanner struct itself, only its inner buffers.
void rcs_standard_scanner_free(struct rcs_standard_scanner *scanner);

#endif
