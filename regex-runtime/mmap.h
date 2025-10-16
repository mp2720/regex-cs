#ifndef REGEX_CS_RUNTIME_MMAP
#define REGEX_CS_RUNTIME_MMAP

#include "api.h"
#include "common.h"
#include <stddef.h>

RCS_NODISCARD
rcs_error rcs_mmap_for_write(void **out_addr, size_t len);

RCS_NODISCARD
rcs_error rcs_mmap_make_exec(void *addr, size_t len);

void rcs_mmap_free(void *addr, size_t len);

#endif
