#include "mmap.h"

#include <assert.h>
#include <errno.h>
#include <stddef.h>
#include <sys/mman.h>

RCS_NODISCARD
rcs_error rcs_mmap_for_write(void **out_addr, size_t len) {
    *out_addr = mmap(NULL, len, PROT_READ | PROT_WRITE, MAP_ANONYMOUS | MAP_PRIVATE, -1, 0);
    if (*out_addr == MAP_FAILED)
        return RCS_MAKE_ERR_LIBC(errno);
    return RCS_OK;
}

RCS_NODISCARD
rcs_error rcs_mmap_make_exec(void *addr, size_t len) {
    int rc = mprotect(addr, len, PROT_EXEC);
    if (rc < 0)
        return RCS_MAKE_ERR_LIBC(errno);
    return RCS_OK;
}

void rcs_mmap_free(void *addr, size_t len) {
    int rc = munmap(addr, len);
    assert(rc > 0 && "munmap() failed");
}
