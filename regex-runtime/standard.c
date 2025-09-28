#include "standard.h"
#include "common.h"
#include <assert.h>
#include <errno.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdlib.h>

rcs_error rcs_standard_scanner_init(struct rcs_standard_scanner *s, const struct rcs_nfa *nfa) {
    *s = (struct rcs_standard_scanner){0};

    s->nfa = nfa;

    s->states_bm_len = RCS_BITMAP_LEN_BYTES(nfa->states_len);
    for (size_t i = 0; i < 2; ++i) {
        s->states_bm[i] = calloc(s->states_bm_len, sizeof(*s->states_bm[i]) * s->states_bm_len);
        if (s->states_bm[i] == NULL)
            goto malloc_err;
    }

    s->input_buf_index = 0;
    s->input_buf_len = 0;

    return RCS_OK;

malloc_err:
    free(s->states_bm[0]);
    free(s->states_bm[1]);
    return RCS_MAKE_ERR_LIBC(errno);
}

// returns -1 on EOF
static int read_char(struct rcs_standard_scanner *sc, const struct rcs_reader *reader) {
    if (sc->input_buf_index < sc->input_buf_len)
        return reader->buf[sc->input_buf_index++];

    rcs_api_size n = reader->read(reader->arg);
    if (n == 0)
        return -1;
    sc->input_buf_index = 1;
    sc->input_buf_len = n;

    return reader->buf[0];
}

static bool state_matches_char(const struct rcs_nfa_state *state, uint8_t c) {
    for (int i = 0; i < state->ranges_len; ++i) {
        const struct rcs_nfa_char_range range = state->ranges[i];
        bool in_range = range.start <= c && c <= range.end;
        if (in_range == state->inverted_match)
            return false;
    }
    return true;
}

rcs_error rcs_standard_match(
    rcs_api_bool *out_ok,
    struct rcs_standard_scanner *sc,
    const struct rcs_reader *reader
) {
    // RCS_BREAKPOINT();

    int c;
    bool reached_final_last_step = false;
    bool has_active_states = false;

    // activate source states
    for (size_t i = 0; i < sc->nfa->sources_len; ++i) {
        const struct rcs_nfa_state *src = &sc->nfa->states[i];
        if (rcs_nfa_state_is_sink(src)) {
            // source could also be sink
            reached_final_last_step = true;
        } else {
            has_active_states = true;
            rcs_bitmap_set(sc->states_bm[0], sc->nfa->sources[i] - sc->nfa->states);
        }
    }

    // RCS_BREAKPOINT();

    while ((c = read_char(sc, reader)) >= 0 && has_active_states) {
        reached_final_last_step = false;
        has_active_states = false;

        for (size_t i = 0; i < sc->nfa->states_len; ++i) {
            const struct rcs_nfa_state *state = &sc->nfa->states[i];

            if (!rcs_bitmap_get(sc->states_bm[0], i))
                continue;

            assert(!rcs_nfa_state_is_sink(state) && "unexpected sink state");
            assert(!rcs_nfa_state_is_epsilon(state) && "unexpected epsilon state");

            if (!state_matches_char(state, c))
                continue;

            for (size_t j = 0; j < state->next_len; ++j) {
                if (rcs_nfa_state_is_sink(state->next[j])) {
                    reached_final_last_step = true;
                } else {
                    rcs_bitmap_set(sc->states_bm[1], state->next[j] - sc->nfa->states);
                    has_active_states = true;
                }
            }
        }

        rcs_bitmap_clear_all(sc->states_bm[0], sc->states_bm_len);
        // swap
        rcs_bitmap_word *tmp = sc->states_bm[0];
        sc->states_bm[0] = sc->states_bm[1];
        sc->states_bm[1] = tmp;
    }

    *out_ok = reached_final_last_step;

    return RCS_OK;
}

void rcs_standard_scanner_free(struct rcs_standard_scanner *scanner) {
    free(scanner->states_bm[0]);
    free(scanner->states_bm[1]);
    scanner->states_bm[0] = scanner->states_bm[1] = NULL;
}
