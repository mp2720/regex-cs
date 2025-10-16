#include "api.h"
#include "jit.h"
#include "standard.h"
#include <assert.h>
#include <errno.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>

rcs_api_bool rcs_failed(rcs_error err) {
    return err.code != RCS_NO_ERR;
}

const char *rcs_strerror(rcs_error error) {
    switch (error.code) {
    case RCS_NO_ERR:
        return "success";
    case RCS_ERR_LIBC:
        return strerror(error.libc_errno);
    case RCS_ERR_JIT_TOO_LONG_JUMP:
        return "too long jump in jit-generated code (state condition is too big)";
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
        struct rcs_jit_scanner jit;
    } backend;
};

rcs_error rcs_scanner_init(const struct rcs_scanner **out_scanner, const struct rcs_nfa *nfa) {
    rcs_error err = RCS_OK;

    struct rcs_scanner *s = malloc(sizeof(struct rcs_scanner));
    if (s == NULL)
        return RCS_MAKE_ERR_LIBC(errno);

    bool jit_supported = rcs_jit_scanner_init(&err, &s->backend.jit, nfa);

    if (jit_supported) {
        if (rcs_failed(err))
            goto error_free;
        s->backend_type = RCS_JIT;
    } else {
        // fallback to slower standard implementation

        s->backend_type = RCS_STANDARD;
        err = rcs_standard_scanner_init(&s->backend.standard, nfa);
        if (rcs_failed(err))
            goto error_free;
    }

    *out_scanner = s;
    return RCS_OK;

error_free:
    free(s);
    return err;
}

rcs_error
rcs_match(rcs_api_bool *out_ok, struct rcs_scanner *scanner, const struct rcs_reader *reader) {
    switch (scanner->backend_type) {
    case RCS_JIT:
        return rcs_jit_match(out_ok, &scanner->backend.jit, reader);
    case RCS_STANDARD:
        return rcs_standard_match(out_ok, &scanner->backend.standard, reader);
    default:
        assert(0 && "invalid scanner backend type");
    }
}

void rcs_scanner_free(struct rcs_scanner *scanner) {
    switch (scanner->backend_type) {
    case RCS_JIT:
        rcs_jit_scanner_free(&scanner->backend.jit);
        break;
    case RCS_STANDARD:
        rcs_standard_scanner_free(&scanner->backend.standard);
        break;
    default:
        assert(0 && "invalid scanner backend type");
    }
}
