#ifndef REGEX_CS_RUNTIME_AMD64_JIT
#define REGEX_CS_RUNTIME_AMD64_JIT

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

struct rcs_jit_scanner {
    uint64_t initial_states_bitmap[4];
    bool has_accepting_source;
    void *mmap_addr;
    size_t mmap_len;
};

#endif
