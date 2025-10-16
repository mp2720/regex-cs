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

    s->states_bm_len = RCS_BITMAP_LEN_WORDS(nfa->states_len);
    for (size_t i = 0; i < 2; ++i) {
        s->states_bm[i] = malloc(s->states_bm_len * sizeof(*s->states_bm[i]));
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
    for (size_t i = 0; i < state->ranges_len; ++i) {
        const struct rcs_nfa_char_range range = state->ranges[i];
        if (range.start <= c && c <= range.end)
            return !state->inverted_match;
    }
    return state->inverted_match;
}

rcs_error rcs_standard_match(
    rcs_api_bool *out_ok,
    struct rcs_standard_scanner *sc,
    const struct rcs_reader *reader
) {
    bool accepted_last_step = false;
    bool has_active_states = false;

    sc->input_buf_len = 0;
    sc->input_buf_index = 0;

    rcs_bitmap_clear_all(sc->states_bm[0], sc->states_bm_len);
    rcs_bitmap_clear_all(sc->states_bm[1], sc->states_bm_len);
    // activate source states
    for (size_t i = 0; i < sc->nfa->sources_len; ++i) {
        const struct rcs_nfa_state *src = sc->nfa->sources[i];
        if (rcs_nfa_state_is_accept(src)) {
            // source could also be accepting
            // remember that accept state is epsilon, so we don't add it to active states
            accepted_last_step = true;
        } else {
            has_active_states = true;
            rcs_bitmap_set(sc->states_bm[0], sc->nfa->sources[i] - sc->nfa->states);
        }
    }

    while (true) {
        int c_or_eof = read_char(sc, reader);
        if (c_or_eof < 0) {
            *out_ok = accepted_last_step;
            break;
        }

        if (!has_active_states) {
            // no EOF, but nfa is in sink
            *out_ok = false;
            break;
        }

        uint8_t c = c_or_eof;

        accepted_last_step = false;
        has_active_states = false;

        for (size_t i = 0; i < sc->nfa->states_len; ++i) {
            const struct rcs_nfa_state *state = &sc->nfa->states[i];

            if (!rcs_bitmap_get(sc->states_bm[0], i))
                continue;

            assert(!rcs_nfa_state_is_accept(state) && "unexpected accept state");
            assert(!rcs_nfa_state_is_epsilon(state) && "unexpected epsilon state");

            if (!state_matches_char(state, c))
                continue;

            for (size_t j = 0; j < state->next_len; ++j) {
                if (rcs_nfa_state_is_accept(state->next[j])) {
                    accepted_last_step = true;
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

    return RCS_OK;
}

void rcs_standard_scanner_free(struct rcs_standard_scanner *scanner) {
    free(scanner->states_bm[0]);
    free(scanner->states_bm[1]);
    scanner->states_bm[0] = scanner->states_bm[1] = NULL;
}
