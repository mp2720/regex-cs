#ifndef REGEX_CS_RUNTIME_API
#define REGEX_CS_RUNTIME_API

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

// C# doesn't have direct `size_t`, `bool` and `int` (for errno) equivalent.
// I don't want to mess with MarshalAs and hardcode platform-dependent types.
typedef uint32_t rcs_api_size;
typedef uint8_t rcs_api_bool;
typedef int32_t rcs_api_libc_errno;

#define RCS_OK             \
    (rcs_error) {          \
        .code = RCS_NO_ERR \
    }
#define RCS_MAKE_ERR(_code) \
    (rcs_error) {           \
        .code = (_code)     \
    }
#define RCS_MAKE_ERR_LIBC(_errno)                    \
    (rcs_error) {                                    \
        .code = RCS_ERR_LIBC, .libc_errno = (_errno) \
    }

typedef enum {
    RCS_NO_ERR = 0,
    RCS_ERR_LIBC,
    RCS_ERR_READER,
    RCS_ERR_JIT_TOO_LONG_JUMP
} rcs_error_code;

typedef struct {
    uint32_t code;                 // value of type rcs_error_code
    rcs_api_libc_errno libc_errno; // set if code is `RCS_ERR_LIBC`
} rcs_error;

rcs_api_bool rcs_failed(rcs_error err);

const char *rcs_strerror(rcs_error error);

// 8-bit char range `[start..end]` or inverse.
struct rcs_nfa_char_range {
    uint8_t start, end;
};

struct rcs_nfa_state {
    // Only the accepting state may have an empty next list.
    struct rcs_nfa_state **next;
    rcs_api_size next_len;

    // ε-states have empty ranges.
    // Only the accepting state is ε.
    struct rcs_nfa_char_range *ranges;
    rcs_api_size ranges_len;

    // Match chars not in ranges union.
    rcs_api_bool inverted_match;
};

struct rcs_nfa {
    struct rcs_nfa_state *states;
    rcs_api_size states_len;
    struct rcs_nfa_state **sources;
    rcs_api_size sources_len;

    // Must be an ε-state.
    struct rcs_nfa_state *accept;
};

struct rcs_reader {
    // Fill the reader's `buf` and advance.
    // Returns number of bytes read to `buf`, 0 on EOF.
    // NOTE: that this function may change `buf` address.
    rcs_api_size (*read)(void *arg);
    // Go `n` bytes back.
    // Returns false on error.
    // NOTE: this function may change `buf` address.
    rcs_api_bool (*unwind)(uint64_t n);

    // Pointer to a part of buffer that contains read by `read()` bytes.
    // WARNING: any `read()` on `unwind()` call may change this address.
    uint8_t *buf;

    void *arg;
};

struct rcs_scanner;

// Allocates a memory and initiates a scanner for the given `nfa`.
// Free created scanner with `rcs_scanner_free()` after use.
rcs_error rcs_scanner_init(const struct rcs_scanner **scanner, const struct rcs_nfa *nfa);

// Match 8-bit string (may contain 0s).
rcs_error
rcs_match(rcs_api_bool *out_ok, struct rcs_scanner *scanner, const struct rcs_reader *reader);

void rcs_scanner_free(struct rcs_scanner *scanner);

#endif
